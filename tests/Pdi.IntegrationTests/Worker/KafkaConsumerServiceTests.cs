using System.Collections.Concurrent;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pdi.IntegrationTests.Support;
using Pdi.Onboarding.Contracts;
using Pdi.Onboarding.Worker.Kafka;

namespace Pdi.IntegrationTests.Worker;

/// A base genérica de consumer contra o Kafka real: ordem, retry, DLQ, commit e
/// resiliência a tópico inexistente. É o que os 4 consumers do Worker herdam.
public sealed class KafkaConsumerServiceTests
{
  public sealed class TestMessage
  {
    public string Id { get; set; } = string.Empty;
  }

  /// Handler roteirizado: para cada Id, devolve os outcomes na ordem configurada.
  public sealed class Script
  {
    public ConcurrentQueue<string> Handled { get; } = new();
    public Dictionary<string, Queue<ConsumerOutcome>> Outcomes { get; } = [];
  }

  public sealed class ScriptedHandler(Script script) : IMessageHandler<TestMessage>
  {
    public Task<ConsumerOutcome> HandleAsync(TestMessage message, CancellationToken cancellationToken)
    {
      script.Handled.Enqueue(message.Id);
      var outcome = script.Outcomes.TryGetValue(message.Id, out var queue) && queue.Count > 0
          ? queue.Dequeue()
          : ConsumerOutcome.Commit;
      return Task.FromResult(outcome);
    }
  }

  [InfraFact]
  public async Task ShouldRetrySameMessageInOrderAndCommitOnlyAfterSuccess()
  {
    var topic = Infra.Unique("it-consumer");
    var group = Infra.Unique("it-group");
    await KafkaAdmin.CreateTopicAsync(topic);
    var script = new Script();
    script.Outcomes["m1"] = new Queue<ConsumerOutcome>([ConsumerOutcome.Retry, ConsumerOutcome.Retry, ConsumerOutcome.Commit]);

    using var host = await StartHost(topic, group, script);
    await Produce(topic, """{"id":"m1"}""", """{"id":"m2"}""");

    await Eventually.True(() => Task.FromResult(script.Handled.Contains("m2")), TimeSpan.FromSeconds(30));

    // m1 tentou 3 vezes (backoff 1s, 2s) ANTES de m2: a partição espera, a ordem é preservada.
    Assert.Equal(new[] { "m1", "m1", "m1", "m2" }, script.Handled.ToArray());
    await Eventually.True(async () => await KafkaAdmin.CommittedAsync(group, topic) == 2, because: "both messages committed");
    await host.StopAsync();
  }

  [InfraFact]
  public async Task PoisonAndRejectedMessages_ShouldGoToDlqAndPartitionShouldMoveOn()
  {
    var topic = Infra.Unique("it-poison");
    var group = Infra.Unique("it-group");
    await KafkaAdmin.CreateTopicAsync(topic);
    var script = new Script();
    script.Outcomes["bad"] = new Queue<ConsumerOutcome>([ConsumerOutcome.DeadLetter]);

    using var host = await StartHost(topic, group, script);
    await Produce(topic, "isso nao e json", """{"id":"bad"}""", """{"id":"good"}""");

    await Eventually.True(() => Task.FromResult(script.Handled.Contains("good")), TimeSpan.FromSeconds(30));

    var dlq = KafkaProbe.Read(Topics.DeadLetter(topic), count: 2);
    Assert.Equal(2, dlq.Count);
    Assert.Equal("isso nao e json", dlq[0].Message.Value);
    Assert.StartsWith("Invalid JSON", Header(dlq[0], KafkaDeadLetterPublisher.ReasonHeader));
    Assert.Equal(topic, Header(dlq[0], KafkaDeadLetterPublisher.SourceTopicHeader));
    Assert.Equal("0", Header(dlq[0], KafkaDeadLetterPublisher.SourceOffsetHeader));
    Assert.Equal("Rejected by handler", Header(dlq[1], KafkaDeadLetterPublisher.ReasonHeader));

    await Eventually.True(async () => await KafkaAdmin.CommittedAsync(group, topic) == 3, because: "DLQ'd messages are committed too");
    await host.StopAsync();
  }

  [InfraFact]
  public async Task MissingTopic_ShouldNotKillTheHost()
  {
    var script = new Script();
    using var host = await StartHost(Infra.Unique("it-missing"), Infra.Unique("it-group"), script);
    var consumer = host.Services.GetServices<IHostedService>().OfType<BackgroundService>().Single();

    await Task.Delay(TimeSpan.FromSeconds(7));

    Assert.NotNull(consumer.ExecuteTask);
    Assert.False(consumer.ExecuteTask!.IsCompleted, "consumer loop died on a missing topic");
    await host.StopAsync();
  }

  private static async Task<IHost> StartHost(string topic, string group, Script script)
  {
    var builder = Host.CreateApplicationBuilder();
    builder.Services.Configure<KafkaOptions>(o => o.BootstrapServers = Infra.Kafka);
    builder.Services.AddSingleton(script);
    builder.Services.AddKafkaConsumer<TestMessage, ScriptedHandler>(topic, group);
    var host = builder.Build();
    await host.StartAsync();
    return host;
  }

  private static async Task Produce(string topic, params string[] values)
  {
    using var producer = KafkaProbe.Producer();
    foreach (var value in values)
      await producer.ProduceAsync(topic, new Message<string, string> { Key = "k", Value = value });
  }

  private static string Header(ConsumeResult<string, string> result, string name)
      => Encoding.UTF8.GetString(result.Message.Headers.GetLastBytes(name));
}
