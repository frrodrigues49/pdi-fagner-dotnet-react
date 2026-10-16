using Pdi.Onboarding.Contracts;

namespace Pdi.Onboarding.Worker.Kafka;

public interface IEventPublisher
{
  Task PublishAsync<TEvent>(string topic, TEvent @event, CancellationToken cancellationToken)
      where TEvent : IOnboardingEvent;
}