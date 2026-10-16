using Pdi.Onboarding.Contracts;
using Pdi.Onboarding.Worker.Kafka;

namespace Pdi.Onboarding.UnitTests.Fakes;

internal sealed class FakeWorkerEventPublisher : IEventPublisher
{
  public List<(string Topic, IOnboardingEvent Event)> Events { get; } = [];
  public bool Fail { get; set; }

  public Task PublishAsync<TEvent>(string topic, TEvent @event, CancellationToken cancellationToken)
      where TEvent : IOnboardingEvent
  {
    if (Fail)
      throw new InvalidOperationException("Kafka is down");

    Events.Add((topic, @event));
    return Task.CompletedTask;
  }
}