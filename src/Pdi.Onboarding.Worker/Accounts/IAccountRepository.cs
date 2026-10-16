namespace Pdi.Onboarding.Worker.Accounts;

public interface IAccountRepository
{
  Task<IReadOnlyList<Account>> GetByProposalAsync(Guid proposalId, CancellationToken cancellationToken);

  /// Insere todas ou nenhuma (uma transação). false = unique constraint violada:
  /// outro processo inseriu contas desta proposta ao mesmo tempo.
  Task<bool> TryAddAsync(IReadOnlyList<Account> accounts, CancellationToken cancellationToken);
}