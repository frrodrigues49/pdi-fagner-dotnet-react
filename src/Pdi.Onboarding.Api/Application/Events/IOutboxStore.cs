using Pdi.Onboarding.Api.Domain.Entities;

namespace Pdi.Onboarding.Api.Application.Events;

public sealed record PendingOutboxMessage(Guid DocumentId, OutboxMessage Message);

public interface IOutboxStore
{
  Task<IReadOnlyList<PendingOutboxMessage>> GetPendingAsync(int limit, CancellationToken cancellationToken = default);
  Task MarkPublishedAsync(Guid documentId, Guid messageId, CancellationToken cancellationToken = default);
}
