namespace Pdi.Onboarding.Contracts;

public sealed class ProposalFinalizedEvent : IOnboardingEvent
{
  public const int ContractVersion = 1;

  public Guid EventId { get; init; }
  public Guid CorrelationId { get; init; }
  public Guid ProposalId { get; init; }
  public int Version { get; init; }
  public DateTime OccurredAt { get; init; }

  public string FullName { get; init; } = string.Empty;
  public string TaxId { get; init; } = string.Empty;
  public string Email { get; init; } = string.Empty;
  public string Phone { get; init; } = string.Empty;
  public DateOnly BirthDate { get; init; }
  public string Street { get; init; } = string.Empty;
  public string City { get; init; } = string.Empty;
  public string State { get; init; } = string.Empty;
  public string PostalCode { get; init; } = string.Empty;
  public string MotherName { get; init; } = string.Empty;
}