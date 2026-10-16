using System.Text.Json;
using Pdi.Antifraude.Api.Domain.Entities;
using Pdi.Onboarding.Contracts;

namespace Pdi.Antifraude.Api.Application.Events;

public static class Outbox
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

  /// Serializa o evento agora (o payload fica congelado) e usa o CorrelationId como key,
  /// para que todos os eventos de uma proposta caiam na mesma partição.
  public static OutboxMessage Message<TEvent>(string topic, TEvent @event)
      where TEvent : IOnboardingEvent
      => new()
      {
        Id = @event.EventId,
        Topic = topic,
        Key = @event.CorrelationId.ToString(),
        Payload = JsonSerializer.Serialize(@event, JsonOptions),
        CreatedAt = DateTime.UtcNow
      };
}
