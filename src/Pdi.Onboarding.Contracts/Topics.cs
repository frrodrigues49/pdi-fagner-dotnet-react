namespace Pdi.Onboarding.Contracts;

public static class Topics
{
  public const string ProposalFinalized = "proposta-conta-finalizada";
  public const string ProposalApproved = "proposta-conta-aprovada";
  public const string ProposalRejected = "proposta-conta-rejeitada";
  public const string CreateAccount = "criar-conta";

  public const string DeadLetterSuffix = ".dlq";

  /// Tópico para onde vão as mensagens que nunca vão conseguir ser processadas.
  public static string DeadLetter(string topic) => topic + DeadLetterSuffix;
}
