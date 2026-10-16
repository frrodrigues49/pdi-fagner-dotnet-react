namespace Pdi.Antifraude.Api.Application.Events;

public interface IEventPublisher
{
  Task PublishAsync(
      string topic,
      string key,
      string payload,
      CancellationToken cancellationToken = default);
}
