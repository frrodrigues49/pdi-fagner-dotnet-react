using Pdi.Antifraude.Api.Application.Analyses;
using Pdi.Antifraude.Api.Domain.Entities;
using Pdi.Antifraude.Api.Domain.Enums;

namespace Pdi.Antifraude.UnitTests.Fakes;

internal sealed class FakeAnalysisRepository : IAnalysisRepository
{
  public Dictionary<Guid, Analysis> Items { get; } = [];
  public bool SimulateConflict { get; set; }

  public Task<bool> TryInsertAsync(Analysis analysis, CancellationToken cancellationToken = default)
  {
    // Simula o unique index em proposalId
    if (Items.Values.Any(x => x.ProposalId == analysis.ProposalId))
      return Task.FromResult(false);

    Items[analysis.Id] = analysis;
    return Task.FromResult(true);
  }

  public Task<Analysis?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
      => Task.FromResult(Items.GetValueOrDefault(id));

  public Task<IReadOnlyList<Analysis>> ListAsync(AnalysisStatus? status, CancellationToken cancellationToken = default)
      => Task.FromResult<IReadOnlyList<Analysis>>(
          Items.Values.Where(x => status is null || x.Status == status).ToList());

  public Task<bool> TryReplaceAsync(
      Analysis analysis,
      AnalysisStatus expectedStatus,
      CancellationToken cancellationToken = default)
  {
    if (SimulateConflict)
      return Task.FromResult(false);

    Items[analysis.Id] = analysis;
    return Task.FromResult(true);
  }
}
