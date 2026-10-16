namespace Pdi.Antifraude.Api.Application.Analyses;

public sealed class AnalysisResult
{
  public AnalysisOperationStatus Status { get; }
  public IReadOnlyList<string> Errors { get; }
  public AnalysisResponse? Data { get; }

  private AnalysisResult(
      AnalysisOperationStatus status,
      IReadOnlyList<string> errors,
      AnalysisResponse? data)
  {
    Status = status;
    Errors = errors;
    Data = data;
  }

  public static AnalysisResult Ok(AnalysisResponse data)
      => new(AnalysisOperationStatus.Success, [], data);

  public static AnalysisResult Created(AnalysisResponse data)
      => new(AnalysisOperationStatus.Created, [], data);

  public static AnalysisResult AlreadyExists()
      => new(AnalysisOperationStatus.AlreadyExists, [], null);

  public static AnalysisResult NotFound()
      => new(AnalysisOperationStatus.NotFound, [], null);

  public static AnalysisResult Invalid(IReadOnlyList<string> errors)
      => new(AnalysisOperationStatus.ValidationFailed, errors, null);

  public static AnalysisResult WrongStatus(string message)
      => new(AnalysisOperationStatus.InvalidStatus, [message], null);
}