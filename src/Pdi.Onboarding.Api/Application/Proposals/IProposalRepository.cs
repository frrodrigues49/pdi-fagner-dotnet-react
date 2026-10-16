using Pdi.Onboarding.Api.Domain.Entities;
using Pdi.Onboarding.Api.Domain.Enums;

namespace Pdi.Onboarding.Api.Application.Proposals;

public interface IProposalRepository
{
  Task InsertAsync(Proposal proposal, CancellationToken cancellationToken = default);
  Task<Proposal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

  /// Grava somente se o documento ainda estiver em expectedStatus/expectedVersion.
  Task<bool> TryReplaceAsync(
      Proposal proposal,
      ProposalStatus expectedStatus,
      int expectedVersion,
      CancellationToken cancellationToken = default);
}