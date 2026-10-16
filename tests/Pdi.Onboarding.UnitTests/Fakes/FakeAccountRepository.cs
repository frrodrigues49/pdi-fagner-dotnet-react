using Pdi.Onboarding.Worker.Accounts;

namespace Pdi.Onboarding.UnitTests.Fakes;

/// Simula o unique (proposta, produto) e a transação: insere todas ou nenhuma.
internal sealed class FakeAccountRepository : IAccountRepository
{
  public List<Account> Items { get; } = [];
  public bool SimulateConcurrentInsert { get; set; }
  public int AddCalls { get; private set; }

  public Task<IReadOnlyList<Account>> GetByProposalAsync(Guid proposalId, CancellationToken cancellationToken)
      => Task.FromResult<IReadOnlyList<Account>>(Items.Where(a => a.ProposalId == proposalId).ToList());

  public Task<bool> TryAddAsync(IReadOnlyList<Account> accounts, CancellationToken cancellationToken)
  {
    AddCalls++;

    var violatesUnique = accounts.Any(n => Items.Any(e => e.ProposalId == n.ProposalId && e.ProductCode == n.ProductCode));
    if (SimulateConcurrentInsert || violatesUnique)
      return Task.FromResult(false);

    Items.AddRange(accounts);
    return Task.FromResult(true);
  }
}