using Confluent.Kafka;

namespace Pdi.IntegrationTests.Support;

public static class KafkaProbe
{
  public static IProducer<string, string> Producer()
      => new ProducerBuilder<string, string>(new ProducerConfig { BootstrapServers = Infra.Kafka }).Build();

  /// Lê o tópico desde o início, até achar `count` mensagens ou estourar o tempo.
  public static List<ConsumeResult<string, string>> Read(string topic, int count = 1, TimeSpan? timeout = null)
  {
    using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
    {
      BootstrapServers = Infra.Kafka,
      GroupId = Infra.Unique("it-probe"),
      AutoOffsetReset = AutoOffsetReset.Earliest,
      EnableAutoCommit = false,
      AllowAutoCreateTopics = true
    }).Build();

    consumer.Subscribe(topic);
    var results = new List<ConsumeResult<string, string>>();
    var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));

    while (results.Count < count && DateTime.UtcNow < deadline)
    {
      try
      {
        var result = consumer.Consume(TimeSpan.FromMilliseconds(500));
        if (result?.Message is not null)
          results.Add(result);
      }
      catch (ConsumeException ex) when (!ex.Error.IsFatal)
      {
        // tópico ainda sendo criado
      }
    }

    consumer.Close();
    return results;
  }
}

public static class KafkaAdmin
{
  /// Cria um tópico de 1 partição (ordem determinística nos testes).
  public static async Task CreateTopicAsync(string topic)
  {
    using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = Infra.Kafka }).Build();
    await admin.CreateTopicsAsync([new Confluent.Kafka.Admin.TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }]);
  }

  /// Soma dos offsets comitados pelo grupo no tópico (= quantas mensagens ele "deu baixa").
  public static async Task<long> CommittedAsync(string groupId, string topic)
  {
    using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = Infra.Kafka }).Build();
    var result = await admin.ListConsumerGroupOffsetsAsync([new ConsumerGroupTopicPartitions(groupId, null)]);
    return result.SelectMany(g => g.Partitions)
        .Where(p => p.Topic == topic && p.Offset.Value >= 0)
        .Sum(p => p.Offset.Value);
  }
}
