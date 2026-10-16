using Pdi.Onboarding.Api.Domain.Entities;
using Pdi.Onboarding.Api.Domain.Enums;

namespace Pdi.Onboarding.Api.Application.Proposals;

public sealed class ProposalResponse
{
  public Guid Id { get; init; }
  public ProposalStatus Status { get; init; }
  public int Version { get; init; }
  public string? FullName { get; init; }
  public string? TaxId { get; init; }
  public string? Email { get; init; }
  public string? Phone { get; init; }
  public DateOnly? BirthDate { get; init; }
  public string? Street { get; init; }
  public string? City { get; init; }
  public string? State { get; init; }
  public string? PostalCode { get; init; }
  public string? MotherName { get; init; }
  public DateTime CreatedAt { get; init; }
  public DateTime UpdatedAt { get; init; }
  public DateTime? FinalizedAt { get; init; }
  public Guid? AnalysisId { get; init; }
  public string? ApprovedBy { get; init; }
  public DateTime? ApprovedAt { get; init; }
  public DateTime? AccountsRequestedAt { get; init; }
  public string? RejectionReason { get; init; }
  public DateTime? RejectedAt { get; init; }

  public static ProposalResponse From(Proposal proposal) => new()
  {
    Id = proposal.Id,
    Status = proposal.Status,
    Version = proposal.Version,
    FullName = proposal.FullName,
    TaxId = proposal.TaxId,
    Email = proposal.Email,
    Phone = proposal.Phone,
    BirthDate = proposal.BirthDate,
    Street = proposal.Street,
    City = proposal.City,
    State = proposal.State,
    PostalCode = proposal.PostalCode,
    MotherName = proposal.MotherName,
    CreatedAt = proposal.CreatedAt,
    UpdatedAt = proposal.UpdatedAt,
    FinalizedAt = proposal.FinalizedAt,
    AnalysisId = proposal.AnalysisId,
    ApprovedBy = proposal.ApprovedBy,
    ApprovedAt = proposal.ApprovedAt,
    AccountsRequestedAt = proposal.AccountsRequestedAt,
    RejectionReason = proposal.RejectionReason,
    RejectedAt = proposal.RejectedAt
  };
}