namespace Pdi.Onboarding.Api.Application.Proposals;

public sealed class ProposalResult
{
  public ProposalOperationStatus Status { get; }
  public IReadOnlyList<string> Errors { get; }
  public ProposalResponse? Data { get; }

  private ProposalResult(
      ProposalOperationStatus status,
      IReadOnlyList<string> errors,
      ProposalResponse? data)
  {
    Status = status;
    Errors = errors;
    Data = data;
  }

  public static ProposalResult Ok(ProposalResponse data)
      => new(ProposalOperationStatus.Success, [], data);

  public static ProposalResult NotFound()
      => new(ProposalOperationStatus.NotFound, [], null);

  public static ProposalResult Invalid(IReadOnlyList<string> errors)
      => new(ProposalOperationStatus.ValidationFailed, errors, null);

  public static ProposalResult WrongStatus(string message)
      => new(ProposalOperationStatus.InvalidStatus, [message], null);

  public static ProposalResult Conflict(string message)
      => new(ProposalOperationStatus.ConcurrencyConflict, [message], null);
}