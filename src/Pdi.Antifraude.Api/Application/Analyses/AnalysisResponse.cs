using Pdi.Antifraude.Api.Domain.Entities;
using Pdi.Antifraude.Api.Domain.Enums;

namespace Pdi.Antifraude.Api.Application.Analyses;

public sealed record AnalysisResponse(
    Guid Id,
    Guid ProposalId,
    AnalysisStatus Status,
    string FullName,
    string TaxId,
    string Email,
    string Phone,
    DateOnly BirthDate,
    string Street,
    string City,
    string State,
    string PostalCode,
    string MotherName,
    string? RejectionReason,
    string? DecidedBy,
    DateTime CreatedAt,
    DateTime? DecidedAt)
{
  public static AnalysisResponse From(Analysis a) => new(
      a.Id, a.ProposalId, a.Status,
      a.FullName, a.TaxId, a.Email, a.Phone, a.BirthDate,
      a.Street, a.City, a.State, a.PostalCode, a.MotherName,
      a.RejectionReason, a.DecidedBy, a.CreatedAt, a.DecidedAt);
}