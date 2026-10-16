namespace Pdi.Onboarding.Api.Application.Events;

public interface IEventPublisher
{
  Task PublishAsync(
      string topic,
      string key,
      string payload,
      CancellationToken cancellationToken = default);
}
