using System.Collections.Concurrent;

namespace Pdi.IntegrationTests.Support;

public sealed record Published(string Topic, string Key, string Payload);

/// Substitui o Kafka nos testes das APIs: o OutboxDispatcher real roda, mas publica aqui.
/// (O KafkaEventPublisher de verdade é testado à parte, contra o Kafka real.)
public sealed class RecordingOnboardingPublisher : Pdi.Onboarding.Api.Application.Events.IEventPublisher
{
  public ConcurrentQueue<Published> Events { get; } = new();

  public Task PublishAsync(string topic, string key, string payload, CancellationToken cancellationToken = default)
  {
    Events.Enqueue(new Published(topic, key, payload));
    return Task.CompletedTask;
  }
}

public sealed class RecordingAntifraudePublisher : Pdi.Antifraude.Api.Application.Events.IEventPublisher
{
  public ConcurrentQueue<Published> Events { get; } = new();

  public Task PublishAsync(string topic, string key, string payload, CancellationToken cancellationToken = default)
  {
    Events.Enqueue(new Published(topic, key, payload));
    return Task.CompletedTask;
  }
}
