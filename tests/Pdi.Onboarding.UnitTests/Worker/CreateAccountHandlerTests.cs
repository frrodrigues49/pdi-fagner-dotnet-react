using Microsoft.Extensions.Logging.Abstractions;
using Pdi.Onboarding.Contracts;
using Pdi.Onboarding.UnitTests.Fakes;
using Pdi.Onboarding.Worker.Accounts;
using Pdi.Onboarding.Worker.Consumers.CreateAccount;
using Pdi.Onboarding.Worker.Kafka;

namespace Pdi.Onboarding.UnitTests.Worker;

public sealed class CreateAccountHandlerTests
{
  private readonly FakeAccountRepository _accounts = new();
  private readonly CreateAccountHandler _handler;

  public CreateAccountHandlerTests()
  {
    _handler = new CreateAccountHandler(_accounts, NullLogger<CreateAccountHandler>.Instance);
  }

  [Fact]
  public async Task HandleAsync_WhenNew_ShouldCreateOneAccountPerProduct()
  {
    var message = CreateAccount();

    var outcome = await _handler.HandleAsync(message, CancellationToken.None);

    Assert.Equal(ConsumerOutcome.Commit, outcome);
    Assert.Equal([173, 120, 9, 10], _accounts.Items.Select(a => a.ProductCode));
    Assert.All(_accounts.Items, a =>
    {
      Assert.Equal(message.ProposalId, a.ProposalId);
      Assert.Equal(AccountNumberGenerator.Generate(message.ProposalId, a.ProductCode), a.AccountNumber);
      Assert.Equal("Ana Souza", a.HolderName);
      Assert.Equal("52998224725", a.HolderTaxId);
      Assert.Equal(message.EventId, a.SourceEventId);
    });
  }

  [Fact]
  public async Task HandleAsync_WhenProcessedTwice_ShouldNotDuplicate()
  {
    var message = CreateAccount();

    await _handler.HandleAsync(message, CancellationToken.None);
    var replay = await _handler.HandleAsync(message, CancellationToken.None);

    Assert.Equal(ConsumerOutcome.Commit, replay);
    Assert.Equal(4, _accounts.Items.Count);
    Assert.Equal(1, _accounts.AddCalls);
  }

  [Fact]
  public async Task HandleAsync_WhenSomeAccountsExist_ShouldCreateOnlyMissingOnes()
  {
    var message = CreateAccount();
    _accounts.Items.Add(new Account { ProposalId = message.ProposalId, ProductCode = 173 });
    _accounts.Items.Add(new Account { ProposalId = message.ProposalId, ProductCode = 9 });

    var outcome = await _handler.HandleAsync(message, CancellationToken.None);

    Assert.Equal(ConsumerOutcome.Commit, outcome);
    Assert.Equal([120, 10], _accounts.Items.Skip(2).Select(a => a.ProductCode));
  }

  [Fact]
  public async Task HandleAsync_WhenAnotherProcessInsertedAtTheSameTime_ShouldRetry()
  {
    _accounts.SimulateConcurrentInsert = true;

    var outcome = await _handler.HandleAsync(CreateAccount(), CancellationToken.None);

    Assert.Equal(ConsumerOutcome.Retry, outcome);
    Assert.Empty(_accounts.Items);
  }

  [Fact]
  public async Task HandleAsync_WhenTaxIdHasPunctuation_ShouldStoreDigitsOnly()
  {
    await _handler.HandleAsync(CreateAccount(taxId: "529.982.247-25"), CancellationToken.None);

    Assert.All(_accounts.Items, a => Assert.Equal("52998224725", a.HolderTaxId));
  }

  [Theory]
  [MemberData(nameof(InvalidMessages))]
  public async Task HandleAsync_WhenMessageIsInvalid_ShouldDeadLetterWithoutTouchingDatabase(CreateAccountEvent message)
  {
    var outcome = await _handler.HandleAsync(message, CancellationToken.None);

    Assert.Equal(ConsumerOutcome.DeadLetter, outcome);
    Assert.Equal(0, _accounts.AddCalls);
  }

  public static TheoryData<CreateAccountEvent> InvalidMessages() => new()
  {
    CreateAccount(products: []),
    CreateAccount(products: [173, 999]),
    CreateAccount(taxId: "123"),
    CreateAccount(fullName: " "),
    CreateAccount(proposalId: Guid.Empty)
  };

  private static CreateAccountEvent CreateAccount(
      Guid? proposalId = null,
      string fullName = "Ana Souza",
      string taxId = "52998224725",
      IReadOnlyList<int>? products = null)
  {
    var id = proposalId ?? Guid.NewGuid();
    return new CreateAccountEvent
    {
      EventId = Guid.NewGuid(),
      CorrelationId = id,
      ProposalId = id,
      Version = CreateAccountEvent.ContractVersion,
      OccurredAt = DateTime.UtcNow,
      FullName = fullName,
      TaxId = taxId,
      ProductCodes = products ?? ProductCodes.All
    };
  }
}