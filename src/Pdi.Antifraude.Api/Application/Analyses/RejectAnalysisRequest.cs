namespace Pdi.Antifraude.Api.Application.Analyses;

public sealed class RejectAnalysisRequest
{
  public string Reason { get; set; } = string.Empty;
}
