namespace Pdi.Onboarding.Worker.Proposals;

/// Cada Try* é uma transição condicional: só grava se o documento ainda estiver
/// no status de origem. Retorna false se outro processo chegou antes.
public interface IProposalStore
{
  Task<ProposalDocument?> GetAsync(Guid id, CancellationToken cancellationToken);

  /// Finalized → Approved
  Task<bool> TryApproveAsync(Guid id, Guid analysisId, string approvedBy, DateTime at, CancellationToken cancellationToken);

  /// Approved → AccountsRequested
  Task<bool> TryMarkAccountsRequestedAsync(Guid id, DateTime at, CancellationToken cancellationToken);

  /// Finalized → Rejected
  Task<bool> TryRejectAsync(Guid id, Guid analysisId, string reason, DateTime at, CancellationToken cancellationToken);
}