using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Pdi.Onboarding.Contracts;

namespace Pdi.Onboarding.Worker.Kafka;

/// Copia a mensagem original (mesma key e value) para <tópico>.dlq, com headers
/// dizendo de onde veio e por que falhou. Dá para inspecionar e reprocessar depois.
public sealed class KafkaDeadLetterPublisher : IDeadLetterPublisher, IDisposable
{
  public const string ReasonHeader = "dlq-reason";
  public const string SourceTopicHeader = "dlq-source-topic";
  public const string SourcePartitionHeader = "dlq-source-partition";
  public const string SourceOffsetHeader = "dlq-source-offset";

  private readonly IProducer<string, string> _producer;
  private readonly ILogger<KafkaDeadLetterPublisher> _logger;

  public KafkaDeadLetterPublisher(
      IOptions<KafkaOptions> options,
      ILogger<KafkaDeadLetterPublisher> logger)
  {
    _logger = logger;
    _producer = new ProducerBuilder<string, string>(new ProducerConfig
    {
      BootstrapServers = options.Value.BootstrapServers,
      Acks = Acks.All,
      MessageTimeoutMs = 10_000
    }).Build();
  }

  public async Task PublishAsync(
      ConsumeResult<string, string> source,
      string reason,
      CancellationToken cancellationToken)
  {
    var deadLetterTopic = Topics.DeadLetter(source.Topic);

    await _producer.ProduceAsync(deadLetterTopic, new Message<string, string>
    {
      Key = source.Message.Key,
      Value = source.Message.Value,
      Headers = new Headers
      {
        { ReasonHeader, Encoding.UTF8.GetBytes(reason) },
        { SourceTopicHeader, Encoding.UTF8.GetBytes(source.Topic) },
        { SourcePartitionHeader, Encoding.UTF8.GetBytes(source.Partition.Value.ToString()) },
        { SourceOffsetHeader, Encoding.UTF8.GetBytes(source.Offset.Value.ToString()) }
      }
    }, cancellationToken);

    _logger.LogWarning(
        "Message {Topic}[{Partition}]@{Offset} sent to {DeadLetterTopic}: {Reason}",
        source.Topic,
        source.Partition.Value,
        source.Offset.Value,
        deadLetterTopic,
        reason);
  }

  public void Dispose()
  {
    _producer.Flush(TimeSpan.FromSeconds(5));
    _producer.Dispose();
  }
}
