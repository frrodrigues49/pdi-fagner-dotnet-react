using Pdi.Antifraude.Api.Domain.Enums;

namespace Pdi.Antifraude.Api.Application.Analyses;

public interface IAnalysisService
{
  Task<AnalysisResult> CreateAsync(CreateAnalysisRequest request, CancellationToken cancellationToken = default);
  Task<IReadOnlyList<AnalysisResponse>> ListAsync(AnalysisStatus? status, CancellationToken cancellationToken = default);
  Task<AnalysisResult> GetAsync(Guid id, CancellationToken cancellationToken = default);
  /// decidedBy = usuário autenticado (claim "sub" do JWT).
  Task<AnalysisResult> ApproveAsync(Guid id, string decidedBy, CancellationToken cancellationToken = default);
  Task<AnalysisResult> RejectAsync(Guid id, RejectAnalysisRequest request, string decidedBy, CancellationToken cancellationToken = default);
}