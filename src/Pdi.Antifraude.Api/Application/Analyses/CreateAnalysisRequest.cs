namespace Pdi.Antifraude.Api.Application.Analyses;

public sealed class CreateAnalysisRequest
{
  public Guid EventId { get; set; }
  public Guid CorrelationId { get; set; }
  public Guid ProposalId { get; set; }

  public string FullName { get; set; } = string.Empty;
  public string TaxId { get; set; } = string.Empty;
  public string Email { get; set; } = string.Empty;
  public string Phone { get; set; } = string.Empty;
  public DateOnly BirthDate { get; set; }
  public string Street { get; set; } = string.Empty;
  public string City { get; set; } = string.Empty;
  public string State { get; set; } = string.Empty;
  public string PostalCode { get; set; } = string.Empty;
  public string MotherName { get; set; } = string.Empty;
}
