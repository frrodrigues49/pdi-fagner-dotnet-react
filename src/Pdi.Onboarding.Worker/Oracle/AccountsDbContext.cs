using Microsoft.EntityFrameworkCore;
using Pdi.Onboarding.Worker.Accounts;

namespace Pdi.Onboarding.Worker.Oracle;

public sealed class AccountsDbContext : DbContext
{
  public AccountsDbContext(DbContextOptions<AccountsDbContext> options)
      : base(options)
  {
  }

  public DbSet<Account> Accounts => Set<Account>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    // Nomes em MAIÚSCULAS: no Oracle dá para consultar sem aspas (SELECT * FROM ACCOUNTS).
    var account = modelBuilder.Entity<Account>();

    account.ToTable("ACCOUNTS");
    account.HasKey(x => x.Id).HasName("PK_ACCOUNTS");

    account.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
    account.Property(x => x.ProposalId).HasColumnName("PROPOSAL_ID");
    account.Property(x => x.ProductCode).HasColumnName("PRODUCT_CODE");
    account.Property(x => x.AccountNumber).HasColumnName("ACCOUNT_NUMBER")
        .HasMaxLength(AccountNumberGenerator.Length).IsRequired();
    account.Property(x => x.HolderName).HasColumnName("HOLDER_NAME").HasMaxLength(200).IsRequired();
    account.Property(x => x.HolderTaxId).HasColumnName("HOLDER_TAX_ID").HasMaxLength(11).IsRequired();
    account.Property(x => x.SourceEventId).HasColumnName("SOURCE_EVENT_ID");
    account.Property(x => x.CreatedAt).HasColumnName("CREATED_AT");

    // A garantia final de idempotência: o banco recusa a 2ª conta do mesmo produto
    // para a mesma proposta, não importa quantas vezes a mensagem chegue.
    account.HasIndex(x => new { x.ProposalId, x.ProductCode })
        .IsUnique()
        .HasDatabaseName("UX_ACCOUNTS_PROPOSAL_PRODUCT");

    account.HasIndex(x => x.AccountNumber)
        .IsUnique()
        .HasDatabaseName("UX_ACCOUNTS_NUMBER");
  }
}
