using Pdi.Onboarding.Worker.Proposals;

namespace Pdi.Onboarding.UnitTests.Fakes;

/// Simula as transições condicionais do Mongo: só muda se o status de origem bater.
internal sealed class FakeProposalStore : IProposalStore
{
  public Dictionary<Guid, ProposalDocument> Items { get; } = [];
  public bool SimulateConflict { get; set; }

  public ProposalDocument Add(ProposalStatus status)
  {
    var proposal = new ProposalDocument
    {
      Id = Guid.NewGuid(),
      Status = status,
      Version = 2,
      FullName = "Ana Souza",
      TaxId = "52998224725"
    };
    Items[proposal.Id] = proposal;
    return proposal;
  }

  // Devolve uma CÓPIA, como o Mongo: mudar o objeto lido não muda o "banco".
  public Task<ProposalDocument?> GetAsync(Guid id, CancellationToken cancellationToken)
      => Task.FromResult(Items.TryGetValue(id, out var p) ? Copy(p) : null);

  public Task<bool> TryApproveAsync(Guid id, Guid analysisId, string approvedBy, DateTime at, CancellationToken cancellationToken)
      => Transition(id, ProposalStatus.Finalized, p =>
      {
        p.Status = ProposalStatus.Approved;
        p.AnalysisId = analysisId;
        p.ApprovedBy = approvedBy;
        p.ApprovedAt = at;
      });

  public Task<bool> TryMarkAccountsRequestedAsync(Guid id, DateTime at, CancellationToken cancellationToken)
      => Transition(id, ProposalStatus.Approved, p =>
      {
        p.Status = ProposalStatus.AccountsRequested;
        p.AccountsRequestedAt = at;
      });

  public Task<bool> TryRejectAsync(Guid id, Guid analysisId, string reason, DateTime at, CancellationToken cancellationToken)
      => Transition(id, ProposalStatus.Finalized, p =>
      {
        p.Status = ProposalStatus.Rejected;
        p.AnalysisId = analysisId;
        p.RejectionReason = reason;
        p.RejectedAt = at;
      });

  private Task<bool> Transition(Guid id, ProposalStatus from, Action<ProposalDocument> apply)
  {
    if (SimulateConflict || !Items.TryGetValue(id, out var p) || p.Status != from)
      return Task.FromResult(false);

    apply(p);
    p.Version++;
    return Task.FromResult(true);
  }

  private static ProposalDocument Copy(ProposalDocument p) => new()
  {
    Id = p.Id,
    Status = p.Status,
    Version = p.Version,
    FullName = p.FullName,
    TaxId = p.TaxId,
    AnalysisId = p.AnalysisId,
    ApprovedBy = p.ApprovedBy,
    ApprovedAt = p.ApprovedAt,
    AccountsRequestedAt = p.AccountsRequestedAt,
    RejectionReason = p.RejectionReason,
    RejectedAt = p.RejectedAt,
    UpdatedAt = p.UpdatedAt
  };
}