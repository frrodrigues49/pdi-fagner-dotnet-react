using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace Pdi.Onboarding.Worker.Kafka;

public sealed class KafkaConsumerService<TEvent> : BackgroundService
    where TEvent : class
{
  private static readonly TimeSpan ConsumeErrorDelay = TimeSpan.FromSeconds(5);
  private static readonly TimeSpan PollWhilePaused = TimeSpan.FromMilliseconds(200);

  private readonly IServiceScopeFactory _scopeFactory;
  private readonly IDeadLetterPublisher _deadLetters;
  private readonly KafkaOptions _options;
  private readonly KafkaConsumerRegistration _registration;
  private readonly ILogger<KafkaConsumerService<TEvent>> _logger;

  public KafkaConsumerService(
      IServiceScopeFactory scopeFactory,
      IDeadLetterPublisher deadLetters,
      IOptions<KafkaOptions> options,
      KafkaConsumerRegistration registration,
      ILogger<KafkaConsumerService<TEvent>> logger)
  {
    _scopeFactory = scopeFactory;
    _deadLetters = deadLetters;
    _options = options.Value;
    _registration = registration;
    _logger = logger;
  }

  // Consume() é BLOQUEANTE. Se rodasse direto aqui, travaria o startup do host
  // (o host só segue quando o ExecuteAsync chega no primeiro await de verdade).
  protected override Task ExecuteAsync(CancellationToken stoppingToken)
      => Task.Run(() => ConsumeLoopAsync(stoppingToken), stoppingToken);

  private async Task ConsumeLoopAsync(CancellationToken stoppingToken)
  {
    var config = new ConsumerConfig
    {
      BootstrapServers = _options.BootstrapServers,
      GroupId = _registration.GroupId,
      AutoOffsetReset = AutoOffsetReset.Earliest,
      EnableAutoCommit = false
    };

    using var consumer = new ConsumerBuilder<string, string>(config).Build();
    consumer.Subscribe(_registration.Topic);

    _logger.LogInformation(
        "Consumer {GroupId} listening to {Topic}",
        _registration.GroupId,
        _registration.Topic);

    try
    {
      while (!stoppingToken.IsCancellationRequested)
      {
        ConsumeResult<string, string> result;
        try
        {
          result = consumer.Consume(stoppingToken);
        }
        catch (ConsumeException ex) when (!ex.Error.IsFatal)
        {
          // Ex.: tópico ainda não criado. O consumer se recupera sozinho quando ele surgir.
          // Erro fatal (credencial, config) sobe e derruba o processo: esperar não resolve.
          _logger.LogWarning(
              "Consume error on {Topic}: {Reason}; retrying in {Delay}",
              _registration.Topic,
              ex.Error.Reason,
              ConsumeErrorDelay);
          await Task.Delay(ConsumeErrorDelay, stoppingToken);
          continue;
        }

        await ProcessUntilDoneAsync(consumer, result, stoppingToken);

        TryCommit(consumer, result);
      }
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
      // desligamento normal
    }
    finally
    {
      consumer.Close();
    }
  }

  // Só retorna quando a mensagem pode ser comitada: processada, replay ou enviada para a DLQ.
  // Enquanto o handler pedir Retry (ou lançar exceção), tenta de novo a MESMA mensagem com
  // backoff exponencial. Isso segura a partição de propósito: preserva a ordem da proposta.
  private async Task ProcessUntilDoneAsync(
      IConsumer<string, string> consumer,
      ConsumeResult<string, string> result,
      CancellationToken stoppingToken)
  {
    var (message, invalidReason) = Deserialize(result);

    for (var attempt = 1; ; attempt++)
    {
      try
      {
        if (message is null)
        {
          await _deadLetters.PublishAsync(result, invalidReason!, stoppingToken);
          return;
        }

        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<TEvent>>();

        var outcome = await handler.HandleAsync(message, stoppingToken);
        if (outcome == ConsumerOutcome.Commit)
          return;

        if (outcome == ConsumerOutcome.DeadLetter)
        {
          await _deadLetters.PublishAsync(result, "Rejected by handler", stoppingToken);
          return;
        }

        _logger.LogWarning(
            "Handler asked to retry offset {Offset} on {Topic} (attempt {Attempt})",
            result.Offset.Value,
            result.Topic,
            attempt);
      }
      catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
      {
        // Inclui falha ao publicar na DLQ: nesse caso também não pode comitar.
        _logger.LogError(
            ex,
            "Failed to handle offset {Offset} on {Topic} (attempt {Attempt})",
            result.Offset.Value,
            result.Topic,
            attempt);
      }

      WaitKeepingGroupMembership(consumer, RetryBackoff.For(attempt), stoppingToken);
    }
  }

  // Um Task.Delay longo aqui faria o consumer passar de max.poll.interval.ms (5 min) sem
  // chamar Consume(), e o Kafka o expulsaria do grupo. Então pausamos as partições (não
  // chega mensagem nova) e seguimos chamando Consume(): o consumer continua vivo no grupo.
  private void WaitKeepingGroupMembership(
      IConsumer<string, string> consumer,
      TimeSpan delay,
      CancellationToken stoppingToken)
  {
    var until = DateTime.UtcNow + delay;
    consumer.Pause(consumer.Assignment);

    try
    {
      while (DateTime.UtcNow < until)
      {
        stoppingToken.ThrowIfCancellationRequested();

        try
        {
          var unexpected = consumer.Consume(PollWhilePaused);
          if (unexpected is not null)
          {
            // Partição recém-atribuída num rebalance não vem pausada. Devolve a
            // mensagem (Seek) para ser lida depois, na ordem, e pausa de novo.
            consumer.Seek(unexpected.TopicPartitionOffset);
            consumer.Pause(consumer.Assignment);
          }
        }
        catch (ConsumeException ex) when (!ex.Error.IsFatal)
        {
          _logger.LogDebug("Ignoring consume error while paused: {Reason}", ex.Error.Reason);
        }
      }
    }
    finally
    {
      consumer.Resume(consumer.Assignment);
    }
  }

  // JSON quebrado nunca vai passar: é poison message e vai para a DLQ.
  private (TEvent? Message, string? InvalidReason) Deserialize(ConsumeResult<string, string> result)
  {
    if (result.Message.Value is null)
      return (null, "Empty message");

    try
    {
      var message = JsonSerializer.Deserialize<TEvent>(result.Message.Value, KafkaJson.Options);
      return message is null ? (null, "Empty message") : (message, null);
    }
    catch (JsonException ex)
    {
      _logger.LogError(ex, "Invalid JSON at offset {Offset} on {Topic}", result.Offset.Value, result.Topic);
      return (null, $"Invalid JSON: {ex.Message}");
    }
  }

  // Se o commit falhar (ex.: rebalance tirou a partição deste consumer), a mensagem
  // será entregue de novo. Não tem problema: o processamento é idempotente.
  private void TryCommit(IConsumer<string, string> consumer, ConsumeResult<string, string> result)
  {
    try
    {
      consumer.Commit(result);
    }
    catch (KafkaException ex)
    {
      _logger.LogWarning(ex, "Commit failed for offset {Offset}; message will be redelivered", result.Offset.Value);
    }
  }
}
