namespace Pdi.Onboarding.Worker.Proposals;

/// Espelho do enum da API Onboarding. No Mongo o status é gravado como TEXTO,
/// então o que precisa bater é o NOME de cada valor, não o número.
public enum ProposalStatus
{
  Draft = 1,
  Finalized = 2,
  Approved = 3,
  AccountsRequested = 4,
  Rejected = 5
}