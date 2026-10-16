namespace Pdi.Antifraude.Api.Application.Analyses;

public sealed class CreateAnalysisValidator
{
  public IReadOnlyList<string> Validate(CreateAnalysisRequest request)
  {
    var errors = new List<string>();

    if (request.EventId == Guid.Empty)
      errors.Add("EventId is required.");

    if (request.ProposalId == Guid.Empty)
      errors.Add("ProposalId is required.");

    if (string.IsNullOrWhiteSpace(request.FullName))
      errors.Add("FullName is required.");

    if (string.IsNullOrWhiteSpace(request.TaxId))
      errors.Add("TaxId is required.");

    return errors;
  }
}