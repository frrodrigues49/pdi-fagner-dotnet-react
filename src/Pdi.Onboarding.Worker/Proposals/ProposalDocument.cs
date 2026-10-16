namespace Pdi.Onboarding.Worker.Proposals;

/// Visão do Worker sobre o documento da coleção precadastro.proposals.
/// Tem só os campos que o Worker lê ou escreve; o resto do documento é preservado
/// porque o Worker faz $set (update parcial), nunca ReplaceOne.
public sealed class ProposalDocument
{
  public Guid Id { get; set; }
  public ProposalStatus Status { get; set; }
  public int Version { get; set; }

  public string? FullName { get; set; }
  public string? TaxId { get; set; }

  public Guid? AnalysisId { get; set; }
  public string? ApprovedBy { get; set; }
  public DateTime? ApprovedAt { get; set; }
  public DateTime? AccountsRequestedAt { get; set; }
  public string? RejectionReason { get; set; }
  public DateTime? RejectedAt { get; set; }
  public DateTime UpdatedAt { get; set; }
}
