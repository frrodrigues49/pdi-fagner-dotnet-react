namespace Pdi.Onboarding.Contracts;

public sealed class CreateAccountEvent : IOnboardingEvent
{
  public const int ContractVersion = 1;

  public Guid EventId { get; init; }
  public Guid CorrelationId { get; init; }
  public Guid ProposalId { get; init; }
  public int Version { get; init; }
  public DateTime OccurredAt { get; init; }

  public string FullName { get; init; } = string.Empty;
  public string TaxId { get; init; } = string.Empty;
  public IReadOnlyList<int> ProductCodes { get; init; } = [];
}