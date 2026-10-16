namespace Pdi.Onboarding.Api.Application.Proposals;

public enum ProposalOperationStatus
{
  Success,
  NotFound,
  ValidationFailed,
  InvalidStatus,
  ConcurrencyConflict
}