using Microsoft.EntityFrameworkCore;
using Pdi.IntegrationTests.Support;
using Pdi.Onboarding.Worker.Accounts;
using Pdi.Onboarding.Worker.Oracle;

namespace Pdi.IntegrationTests.Worker;

/// Oracle real: migration, transação e o unique (PROPOSAL_ID, PRODUCT_CODE).
public sealed class EfAccountRepositoryTests : IAsyncLifetime
{
  private readonly Guid _proposalId = Guid.NewGuid();
  private AccountsDbContext _db = null!;

  public async Task InitializeAsync()
  {
    if (!Infra.IsAvailable && !Infra.Required)
      return;

    _db = new AccountsDbContext(new DbContextOptionsBuilder<AccountsDbContext>().UseOracle(Infra.Oracle).Options);
    await _db.Database.MigrateAsync();
  }

  [InfraFact]
  public async Task TryAdd_ShouldInsertAllInOneTransaction_AndRefuseDuplicates()
  {
    var repository = new EfAccountRepository(_db);

    Assert.True(await repository.TryAddAsync(ProductCodes(_proposalId, 173, 120, 9, 10), CancellationToken.None));

    // 120 já existe: o INSERT inteiro é desfeito (o 999 também não entra).
    Assert.False(await repository.TryAddAsync(ProductCodes(_proposalId, 120, 999), CancellationToken.None));

    var stored = await repository.GetByProposalAsync(_proposalId, CancellationToken.None);
    Assert.Equal([9, 10, 120, 173], stored.Select(a => a.ProductCode).Order());
    Assert.All(stored, a => Assert.True(AccountNumberGenerator.IsValid(a.AccountNumber)));
  }

  [InfraFact]
  public void DesignTimeFactory_ShouldBuildOracleContext()
  {
    using var context = new AccountsDbContextFactory().CreateDbContext([]);

    Assert.Equal("Oracle.EntityFrameworkCore", context.Database.ProviderName);
    Assert.NotNull(context.Model.FindEntityType(typeof(Account)));
  }

  private static List<Account> ProductCodes(Guid proposalId, params int[] codes)
      => codes.Select(code => new Account
      {
        ProposalId = proposalId,
        ProductCode = code,
        AccountNumber = AccountNumberGenerator.Generate(proposalId, code),
        HolderName = "Integracao Oracle",
        HolderTaxId = "52998224725",
        SourceEventId = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow
      }).ToList();

  public async Task DisposeAsync()
  {
    if (_db is null)
      return;

    await _db.Accounts.Where(a => a.ProposalId == _proposalId).ExecuteDeleteAsync();
    await _db.DisposeAsync();
  }
}
