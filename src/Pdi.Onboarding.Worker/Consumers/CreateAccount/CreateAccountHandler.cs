using Pdi.Onboarding.Contracts;
using Pdi.Onboarding.Worker.Accounts;
using Pdi.Onboarding.Worker.Kafka;

namespace Pdi.Onboarding.Worker.Consumers.CreateAccount;

/// criar-conta → Oracle: uma conta por produto (173, 120, 9, 10).
///
/// Idempotência em duas camadas:
///   1. Antes de inserir, vê quais produtos já têm conta e cria só os que faltam.
///   2. Se dois processos passarem juntos pela etapa 1, o unique (proposta, produto)
///      no banco barra o segundo; ele tenta de novo, relê, vê tudo criado e comita.
public sealed class CreateAccountHandler : IMessageHandler<CreateAccountEvent>
{
  private readonly IAccountRepository _accounts;
  private readonly ILogger<CreateAccountHandler> _logger;

  public CreateAccountHandler(IAccountRepository accounts, ILogger<CreateAccountHandler> logger)
  {
    _accounts = accounts;
    _logger = logger;
  }

  public async Task<ConsumerOutcome> HandleAsync(CreateAccountEvent message, CancellationToken cancellationToken)
  {
    var taxId = new string(message.TaxId.Where(char.IsDigit).ToArray());

    var error = Validate(message, taxId);
    if (error is not null)
    {
      _logger.LogError("Invalid criar-conta for proposal {ProposalId}: {Error}", message.ProposalId, error);
      return ConsumerOutcome.DeadLetter;
    }

    var existing = await _accounts.GetByProposalAsync(message.ProposalId, cancellationToken);
    var missing = message.ProductCodes
        .Distinct()
        .Except(existing.Select(a => a.ProductCode))
        .ToList();

    if (missing.Count == 0)
    {
      _logger.LogInformation("Proposal {ProposalId} already has all accounts; replay ignored", message.ProposalId);
      return ConsumerOutcome.Commit;
    }

    var now = DateTime.UtcNow;
    var accounts = missing.Select(productCode => new Account
    {
      ProposalId = message.ProposalId,
      ProductCode = productCode,
      AccountNumber = AccountNumberGenerator.Generate(message.ProposalId, productCode),
      HolderName = message.FullName.Trim(),
      HolderTaxId = taxId,
      SourceEventId = message.EventId,
      CreatedAt = now
    }).ToList();

    if (!await _accounts.TryAddAsync(accounts, cancellationToken))
    {
      _logger.LogWarning("Concurrent account creation for proposal {ProposalId}; will re-check", message.ProposalId);
      return ConsumerOutcome.Retry;
    }

    _logger.LogInformation(
        "Created {Count} accounts for proposal {ProposalId}: {Accounts}",
        accounts.Count,
        message.ProposalId,
        string.Join(", ", accounts.Select(a => $"{a.ProductCode}={a.AccountNumber}")));

    return ConsumerOutcome.Commit;
  }

  // Mensagem errada nunca vai passar: DLQ. Por isso validamos antes de tocar no banco.
  private static string? Validate(CreateAccountEvent message, string taxId)
  {
    if (message.ProposalId == Guid.Empty)
      return "ProposalId is required.";

    if (string.IsNullOrWhiteSpace(message.FullName))
      return "FullName is required.";

    if (taxId.Length != 11)
      return "TaxId must have 11 digits.";

    if (message.ProductCodes.Count == 0)
      return "At least one product is required.";

    var unknown = message.ProductCodes.Except(ProductCodes.All).ToList();
    if (unknown.Count > 0)
      return $"Unknown product codes: {string.Join(", ", unknown)}.";

    return null;
  }
}