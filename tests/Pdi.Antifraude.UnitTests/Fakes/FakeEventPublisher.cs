using Pdi.Antifraude.Api.Application.Events;

namespace Pdi.Antifraude.UnitTests.Fakes;

internal sealed class FakeEventPublisher : IEventPublisher
{
  public List<(string Topic, string Key, string Payload)> Events { get; } = [];
  public bool Fail { get; set; }

  public Task PublishAsync(
      string topic,
      string key,
      string payload,
      CancellationToken cancellationToken = default)
  {
    if (Fail)
      throw new InvalidOperationException("Kafka is down");

    Events.Add((topic, key, payload));
    return Task.CompletedTask;
  }
}
