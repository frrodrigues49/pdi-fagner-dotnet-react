using Pdi.Antifraude.Api.Domain.Enums;

namespace Pdi.Antifraude.Api.Domain.Entities;

public sealed class Analysis
{
  public Guid Id { get; set; }
  public Guid ProposalId { get; set; }
  public Guid EventId { get; set; }
  public Guid CorrelationId { get; set; }
  public AnalysisStatus Status { get; set; }

  // Snapshot do cliente no momento da finalização
  public string FullName { get; set; } = string.Empty;
  public string TaxId { get; set; } = string.Empty;
  public string Email { get; set; } = string.Empty;
  public string Phone { get; set; } = string.Empty;
  public DateOnly BirthDate { get; set; }
  public string Street { get; set; } = string.Empty;
  public string City { get; set; } = string.Empty;
  public string State { get; set; } = string.Empty;
  public string PostalCode { get; set; } = string.Empty;
  public string MotherName { get; set; } = string.Empty;

  public string? RejectionReason { get; set; }
  public string? DecidedBy { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime UpdatedAt { get; set; }
  public DateTime? DecidedAt { get; set; }

  /// Outbox: eventos gravados junto com a decisão, publicados pelo OutboxDispatcher.
  public List<OutboxMessage> PendingEvents { get; set; } = [];
}