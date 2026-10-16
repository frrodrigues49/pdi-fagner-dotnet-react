using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pdi.Onboarding.Worker.Oracle;

/// Usado só pelo `dotnet ef` (gerar migrations). Sem isso, a ferramenta tentaria
/// subir o Worker inteiro (Kafka, Mongo...) só para descobrir o DbContext.
/// Gerar migration não conecta no banco; a connection string aqui é só um placeholder.
public sealed class AccountsDbContextFactory : IDesignTimeDbContextFactory<AccountsDbContext>
{
  public AccountsDbContext CreateDbContext(string[] args)
  {
    var options = new DbContextOptionsBuilder<AccountsDbContext>()
        .UseOracle("User Id=pdi;Password=pdi;Data Source=localhost:1521/FREEPDB1")
        .Options;

    return new AccountsDbContext(options);
  }
}