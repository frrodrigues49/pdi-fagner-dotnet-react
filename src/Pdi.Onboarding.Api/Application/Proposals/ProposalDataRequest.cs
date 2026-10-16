namespace Pdi.Onboarding.Api.Application.Proposals;

public sealed class ProposalDataRequest
{
  public string? FullName { get; set; }
  public string? TaxId { get; set; }
  public string? Email { get; set; }
  public string? Phone { get; set; }
  public DateOnly? BirthDate { get; set; }
  public string? Street { get; set; }
  public string? City { get; set; }
  public string? State { get; set; }
  public string? PostalCode { get; set; }
  public string? MotherName { get; set; }
}