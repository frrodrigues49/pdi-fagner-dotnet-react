namespace Pdi.Onboarding.Api.Domain.Entities;

/// Evento que ainda precisa ser publicado no Kafka. Fica gravado DENTRO do documento
/// da proposta, então mudar o status e "agendar" o evento é uma escrita só (atômica).
public sealed class OutboxMessage
{
  public Guid Id { get; set; }
  public string Topic { get; set; } = string.Empty;
  public string Key { get; set; } = string.Empty;
  public string Payload { get; set; } = string.Empty;
  public DateTime CreatedAt { get; set; }
}
