namespace Pdi.Onboarding.Worker.Kafka;

public interface IMessageHandler<in TEvent>
{
  Task<ConsumerOutcome> HandleAsync(TEvent message, CancellationToken cancellationToken);
}