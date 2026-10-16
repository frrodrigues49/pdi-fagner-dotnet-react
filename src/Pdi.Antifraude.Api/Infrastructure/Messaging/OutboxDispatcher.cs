using Pdi.Antifraude.Api.Application.Events;

namespace Pdi.Antifraude.Api.Infrastructure.Messaging;

/// Lê os eventos pendentes gravados nos documentos e publica no Kafka.
/// Garantia: at-least-once. Se cair entre publicar e marcar, o evento sai de novo,
/// e os consumers são idempotentes.
public sealed class OutboxDispatcher : BackgroundService
{
  private const int BatchSize = 50;
  private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(1);
  private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(5);

  private readonly IOutboxStore _store;
  private readonly IEventPublisher _publisher;
  private readonly ILogger<OutboxDispatcher> _logger;

  public OutboxDispatcher(
      IOutboxStore store,
      IEventPublisher publisher,
      ILogger<OutboxDispatcher> logger)
  {
    _store = store;
    _publisher = publisher;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        var published = await DispatchOnceAsync(stoppingToken);
        if (published == 0)
          await Task.Delay(IdleDelay, stoppingToken);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
        break;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Outbox dispatch failed; retrying in {Delay}", ErrorDelay);
        await Task.Delay(ErrorDelay, stoppingToken);
      }
    }
  }

  public async Task<int> DispatchOnceAsync(CancellationToken cancellationToken)
  {
    var pending = await _store.GetPendingAsync(BatchSize, cancellationToken);

    foreach (var item in pending.OrderBy(x => x.Message.CreatedAt))
    {
      await _publisher.PublishAsync(item.Message.Topic, item.Message.Key, item.Message.Payload, cancellationToken);
      await _store.MarkPublishedAsync(item.DocumentId, item.Message.Id, cancellationToken);
    }

    return pending.Count;
  }
}
