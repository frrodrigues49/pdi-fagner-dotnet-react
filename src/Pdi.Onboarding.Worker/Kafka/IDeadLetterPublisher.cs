using Confluent.Kafka;

namespace Pdi.Onboarding.Worker.Kafka;

public interface IDeadLetterPublisher
{
  Task PublishAsync(
      ConsumeResult<string, string> source,
      string reason,
      CancellationToken cancellationToken);
}
