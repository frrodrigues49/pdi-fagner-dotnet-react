using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pdi.IntegrationTests.Support;
using Pdi.Onboarding.Contracts;

namespace Pdi.IntegrationTests.Worker;

/// Os producers reais (das duas APIs e do Worker) publicando no Kafka de verdade.
public sealed class KafkaPublishersTests
{
  [InfraFact]
  public async Task OnboardingAndAntifraudePublishers_ShouldPublishKeyAndPayload()
  {
    var topic = Infra.Unique("it-publish");

    using (var onboarding = new Pdi.Onboarding.Api.Infrastructure.Messaging.KafkaEventPublisher(
               Options.Create(new Pdi.Onboarding.Api.Infrastructure.Messaging.KafkaOptions { BootstrapServers = Infra.Kafka }),
               NullLogger<Pdi.Onboarding.Api.Infrastructure.Messaging.KafkaEventPublisher>.Instance))
      await onboarding.PublishAsync(topic, "proposta-1", """{"from":"onboarding"}""");

    using (var antifraude = new Pdi.Antifraude.Api.Infrastructure.Messaging.KafkaEventPublisher(
               Options.Create(new Pdi.Antifraude.Api.Infrastructure.Messaging.KafkaOptions { BootstrapServers = Infra.Kafka }),
               NullLogger<Pdi.Antifraude.Api.Infrastructure.Messaging.KafkaEventPublisher>.Instance))
      await antifraude.PublishAsync(topic, "proposta-1", """{"from":"antifraude"}""");

    var messages = KafkaProbe.Read(topic, count: 2);
    Assert.Equal(["proposta-1", "proposta-1"], messages.Select(m => m.Message.Key));
    Assert.Equal(["""{"from":"onboarding"}""", """{"from":"antifraude"}"""], messages.Select(m => m.Message.Value));
  }

  [InfraFact]
  public async Task WorkerPublisher_ShouldSerializeCamelCaseAndUseCorrelationIdAsKey()
  {
    var topic = Infra.Unique("it-worker-publish");
    var proposalId = Guid.NewGuid();
    var options = Options.Create(new Pdi.Onboarding.Worker.Kafka.KafkaOptions { BootstrapServers = Infra.Kafka });

    using (var publisher = new Pdi.Onboarding.Worker.Kafka.KafkaEventPublisher(options, NullLogger<Pdi.Onboarding.Worker.Kafka.KafkaEventPublisher>.Instance))
      await publisher.PublishAsync(topic, new CreateAccountEvent
      {
        EventId = Guid.NewGuid(),
        CorrelationId = proposalId,
        ProposalId = proposalId,
        FullName = "Integracao",
        TaxId = "52998224725",
        ProductCodes = ProductCodes.All
      }, CancellationToken.None);

    var message = Assert.Single(KafkaProbe.Read(topic));
    Assert.Equal(proposalId.ToString(), message.Message.Key);
    var json = JsonDocument.Parse(message.Message.Value).RootElement;
    Assert.Equal(proposalId, json.GetProperty("proposalId").GetGuid());
    Assert.Equal([173, 120, 9, 10], json.GetProperty("productCodes").EnumerateArray().Select(e => e.GetInt32()));
  }
}
