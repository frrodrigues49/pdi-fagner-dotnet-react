using System.Text.Json;

namespace Pdi.Onboarding.Worker.Kafka;

public static class KafkaJson
{
  // Web = camelCase + case-insensitive: lê o que o KafkaEventPublisher escreve
  // e envia no formato que as APIs esperam.
  public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}