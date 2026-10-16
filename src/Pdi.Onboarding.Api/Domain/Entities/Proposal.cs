using Pdi.Onboarding.Api.Domain.Enums;

namespace Pdi.Onboarding.Api.Domain.Entities;

public sealed class Proposal
{
  public Guid Id { get; set; }
  public ProposalStatus Status { get; set; }
  public int Version { get; set; }

  public string? FullName { get; set; }
  public string? TaxId { get; set; }
  public string? Email { get; set; }
  public string? Phone { get; set; }
  public DateOnly? BirthDate { get; set; }
  public string? Street { get; set; }
  public string? City { get; set; }
  public string? State { get; set; }
  public string? PostalCode { get; set; }
  public string? MotherName { get; set; }

  public DateTime CreatedAt { get; set; }
  public DateTime UpdatedAt { get; set; }
  public DateTime? FinalizedAt { get; set; }

  // Preenchidos pelo Worker (consumers de aprovada/rejeitada)
  public Guid? AnalysisId { get; set; }
  public string? ApprovedBy { get; set; }
  public DateTime? ApprovedAt { get; set; }
  public DateTime? AccountsRequestedAt { get; set; }
  public string? RejectionReason { get; set; }
  public DateTime? RejectedAt { get; set; }

  /// Outbox: eventos gravados junto com a mudança de status, publicados pelo OutboxDispatcher.
  public List<OutboxMessage> PendingEvents { get; set; } = [];
}