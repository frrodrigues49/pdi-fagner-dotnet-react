namespace Pdi.Onboarding.Contracts;

public sealed class ProposalRejectedEvent : IOnboardingEvent
{
  public const int ContractVersion = 1;

  public Guid EventId { get; init; }
  public Guid CorrelationId { get; init; }
  public Guid ProposalId { get; init; }
  public Guid AnalysisId { get; init; }
  public int Version { get; init; }
  public DateTime OccurredAt { get; init; }
  public string RejectedBy { get; init; } = string.Empty;
  public string Reason { get; init; } = string.Empty;
}
