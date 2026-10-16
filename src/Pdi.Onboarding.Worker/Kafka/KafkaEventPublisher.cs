using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Pdi.Onboarding.Contracts;

namespace Pdi.Onboarding.Worker.Kafka;

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
      MessageTimeoutMs = 10_000
    }).Build();
  }

  public async Task PublishAsync<TEvent>(string topic, TEvent @event, CancellationToken cancellationToken)
      where TEvent : IOnboardingEvent
  {
    var result = await _producer.ProduceAsync(topic, new Message<string, string>
    {
      Key = @event.CorrelationId.ToString(),
      Value = JsonSerializer.Serialize(@event, KafkaJson.Options)
    }, cancellationToken);

    _logger.LogInformation(
        "Event {EventId} published to {Topic} partition {Partition} offset {Offset}",
        @event.EventId,
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