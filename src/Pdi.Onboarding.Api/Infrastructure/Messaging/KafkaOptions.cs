namespace Pdi.Onboarding.Api.Infrastructure.Messaging;

public sealed class KafkaOptions
{
  public const string SectionName = "Kafka";
  public string BootstrapServers { get; set; } = "localhost:9092";
}