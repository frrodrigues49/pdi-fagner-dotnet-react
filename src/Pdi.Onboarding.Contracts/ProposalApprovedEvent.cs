namespace Pdi.Onboarding.Contracts;

public sealed class ProposalApprovedEvent : IOnboardingEvent
{
  public const int ContractVersion = 1;

  public Guid EventId { get; init; }
  public Guid CorrelationId { get; init; }
  public Guid ProposalId { get; init; }
  public Guid AnalysisId { get; init; }
  public int Version { get; init; }
  public DateTime OccurredAt { get; init; }
  public string ApprovedBy { get; init; } = string.Empty;
}