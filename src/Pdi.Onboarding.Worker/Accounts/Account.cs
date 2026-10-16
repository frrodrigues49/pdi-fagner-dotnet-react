namespace Pdi.Onboarding.Worker.Accounts;

/// Uma conta no core (Oracle). Uma proposta aprovada gera uma conta por produto.
public sealed class Account
{
  public long Id { get; set; }
  public Guid ProposalId { get; set; }
  public int ProductCode { get; set; }
  public string AccountNumber { get; set; } = string.Empty;
  public string HolderName { get; set; } = string.Empty;
  public string HolderTaxId { get; set; } = string.Empty;
  public Guid SourceEventId { get; set; }
  public DateTime CreatedAt { get; set; }
}