using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Pdi.Onboarding.Api.Application.Events;

namespace Pdi.Onboarding.Api.Infrastructure.Messaging;

public sealed class KafkaEventPublisher : IEventPublisher, IDisposable
{
  private readonly IProducer<string, string> _producer;
  private readonly ILogger<KafkaEventPublisher> _logger;

  public KafkaEventPublisher(
      IOptions<KafkaOptions> options,
      ILogger<KafkaEventPublisher> logger)
  {
    _logger = logger;
    _producer = new ProducerBuilder<string, string>(new ProducerConfig
    {
      BootstrapServers = options.Value.BootstrapServers,
      Acks = Acks.All,
      // Falha rápido (o padrão é 5 min): o OutboxDispatcher tenta de novo depois.
      MessageTimeoutMs = 10_000
    }).Build();
  }

  public async Task PublishAsync(
      string topic,
      string key,
      string payload,
      CancellationToken cancellationToken = default)
  {
    var result = await _producer.ProduceAsync(
        topic,
        new Message<string, string> { Key = key, Value = payload },
        cancellationToken);

    _logger.LogInformation(
        "Event published to {Topic} partition {Partition} offset {Offset}",
        topic,
        result.Partition.Value,
        result.Offset.Value);
  }

  public void Dispose()
  {
    _producer.Flush(TimeSpan.FromSeconds(5));
    _producer.Dispose();
  }
}
