using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using Pdi.Onboarding.Worker.Accounts;

namespace Pdi.Onboarding.Worker.Oracle;

public sealed class EfAccountRepository : IAccountRepository
{
  // ORA-00001: unique constraint violated
  private const int UniqueConstraintViolated = 1;

  private readonly AccountsDbContext _db;

  public EfAccountRepository(AccountsDbContext db)
  {
    _db = db;
  }

  public async Task<IReadOnlyList<Account>> GetByProposalAsync(Guid proposalId, CancellationToken cancellationToken)
      => await _db.Accounts
          .AsNoTracking()
          .Where(x => x.ProposalId == proposalId)
          .ToListAsync(cancellationToken);

  public async Task<bool> TryAddAsync(IReadOnlyList<Account> accounts, CancellationToken cancellationToken)
  {
    _db.Accounts.AddRange(accounts);

    try
    {
      // SaveChanges com vários INSERTs roda numa transação: entram todas ou nenhuma.
      await _db.SaveChangesAsync(cancellationToken);
      return true;
    }
    catch (DbUpdateException ex) when (ex.InnerException is OracleException { Number: UniqueConstraintViolated })
    {
      _db.ChangeTracker.Clear();
      return false;
    }
  }
}