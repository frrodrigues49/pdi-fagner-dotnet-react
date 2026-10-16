using Pdi.Antifraude.Api.Domain.Entities;
using Pdi.Antifraude.Api.Domain.Enums;

namespace Pdi.Antifraude.Api.Application.Analyses;

public interface IAnalysisRepository
{
  /// false = já existe análise para esse ProposalId (unique index).
  Task<bool> TryInsertAsync(Analysis analysis, CancellationToken cancellationToken = default);
  Task<Analysis?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
  Task<IReadOnlyList<Analysis>> ListAsync(AnalysisStatus? status, CancellationToken cancellationToken = default);

  /// Grava somente se o documento ainda estiver em expectedStatus.
  Task<bool> TryReplaceAsync(
      Analysis analysis,
      AnalysisStatus expectedStatus,
      CancellationToken cancellationToken = default);
}
