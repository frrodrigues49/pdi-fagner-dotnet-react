namespace Pdi.Antifraude.Api.Application.Analyses;

public sealed class RejectAnalysisValidator
{
  public const int MaxReasonLength = 500;

  public IReadOnlyList<string> Validate(RejectAnalysisRequest request)
  {
    var errors = new List<string>();
    var reason = request.Reason?.Trim() ?? string.Empty;

    if (reason.Length < 3)
      errors.Add("Reason is required and must have at least 3 characters.");

    if (reason.Length > MaxReasonLength)
      errors.Add($"Reason must have at most {MaxReasonLength} characters.");

    return errors;
  }
}
