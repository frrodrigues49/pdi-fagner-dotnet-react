namespace Pdi.Antifraude.Api.Application.Analyses;

public enum AnalysisOperationStatus
{
  Success,
  Created,
  AlreadyExists,
  NotFound,
  ValidationFailed,
  InvalidStatus
}