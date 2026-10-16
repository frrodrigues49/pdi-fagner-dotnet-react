using Microsoft.Extensions.Logging.Abstractions;
using Pdi.Onboarding.Contracts;
using Pdi.Onboarding.UnitTests.Fakes;
using Pdi.Onboarding.Worker.Consumers.ProposalApproved;
using Pdi.Onboarding.Worker.Kafka;
using Pdi.Onboarding.Worker.Proposals;

namespace Pdi.Onboarding.UnitTests.Worker;

public sealed class ProposalApprovedHandlerTests
{
  private readonly FakeProposalStore _store = new();
  private readonly FakeWorkerEventPublisher _publisher = new();
  private readonly ProposalApprovedHandler _handler;

  public ProposalApprovedHandlerTests()
  {
    _handler = new ProposalApprovedHandler(_store, _publisher, NullLogger<ProposalApprovedHandler>.Instance);
  }

  [Fact]
  public async Task HandleAsync_WhenFinalized_ShouldApprovePublishCreateAccountAndMarkRequested()
  {
    var proposal = _store.Add(ProposalStatus.Finalized);
    var message = Approved(proposal.Id);

    var outcome = await _handler.HandleAsync(message, CancellationToken.None);

    Assert.Equal(ConsumerOutcome.Commit, outcome);
    var stored = _store.Items[proposal.Id];
    Assert.Equal(ProposalStatus.AccountsRequested, stored.Status);
    Assert.Equal(message.AnalysisId, stored.AnalysisId);
    Assert.Equal("backoffice", stored.ApprovedBy);
    Assert.NotNull(stored.ApprovedAt);
    Assert.NotNull(stored.AccountsRequestedAt);

    var (topic, published) = Assert.Single(_publisher.Events);
    Assert.Equal(Topics.CreateAccount, topic);
    var createAccount = Assert.IsType<CreateAccountEvent>(published);
    Assert.Equal(proposal.Id, createAccount.ProposalId);
    Assert.Equal(message.CorrelationId, createAccount.CorrelationId);
    Assert.Equal("52998224725", createAccount.TaxId);
    Assert.Equal([173, 120, 9, 10], createAccount.ProductCodes);
  }

  [Fact]
  public async Task HandleAsync_WhenAlreadyApprovedButNotPublished_ShouldResumeFromCheckpoint()
  {
    // Simula um crash depois de aprovar e antes de publicar: o replay continua dali.
    var proposal = _store.Add(ProposalStatus.Approved);

    var outcome = await _handler.HandleAsync(Approved(proposal.Id), CancellationToken.None);

    Assert.Equal(ConsumerOutcome.Commit, outcome);
    Assert.Single(_publisher.Events);
    Assert.Equal(ProposalStatus.AccountsRequested, _store.Items[proposal.Id].Status);
  }

  [Fact]
  public async Task HandleAsync_WhenAccountsAlreadyRequested_ShouldCommitWithoutPublishing()
  {
    var proposal = _store.Add(ProposalStatus.AccountsRequested);

    var outcome = await _handler.HandleAsync(Approved(proposal.Id), CancellationToken.None);

    Assert.Equal(ConsumerOutcome.Commit, outcome);
    Assert.Empty(_publisher.Events);
  }

  [Fact]
  public async Task HandleAsync_WhenProcessedTwice_ShouldPublishOnlyOnce()
  {
    var proposal = _store.Add(ProposalStatus.Finalized);
    var message = Approved(proposal.Id);

    await _handler.HandleAsync(message, CancellationToken.None);
    await _handler.HandleAsync(message, CancellationToken.None);

    Assert.Single(_publisher.Events);
  }

  [Theory]
  [InlineData(ProposalStatus.Draft)]
  [InlineData(ProposalStatus.Rejected)]
  public async Task HandleAsync_WhenStatusCannotBeApproved_ShouldDeadLetter(ProposalStatus status)
  {
    var proposal = _store.Add(status);

    var outcome = await _handler.HandleAsync(Approved(proposal.Id), CancellationToken.None);

    Assert.Equal(ConsumerOutcome.DeadLetter, outcome);
    Assert.Equal(status, _store.Items[proposal.Id].Status);
    Assert.Empty(_publisher.Events);
  }

  [Fact]
  public async Task HandleAsync_WhenProposalDoesNotExist_ShouldDeadLetter()
  {
    var outcome = await _handler.HandleAsync(Approved(Guid.NewGuid()), CancellationToken.None);

    Assert.Equal(ConsumerOutcome.DeadLetter, outcome);
  }

  [Fact]
  public async Task HandleAsync_WhenProposalChangedConcurrently_ShouldRetry()
  {
    var proposal = _store.Add(ProposalStatus.Finalized);
    _store.SimulateConflict = true;

    var outcome = await _handler.HandleAsync(Approved(proposal.Id), CancellationToken.None);

    Assert.Equal(ConsumerOutcome.Retry, outcome);
    Assert.Empty(_publisher.Events);
  }

  [Fact]
  public async Task HandleAsync_WhenKafkaFails_ShouldStayApprovedSoReplayPublishes()
  {
    var proposal = _store.Add(ProposalStatus.Finalized);
    _publisher.Fail = true;

    await Assert.ThrowsAsync<InvalidOperationException>(
        () => _handler.HandleAsync(Approved(proposal.Id), CancellationToken.None));

    // Não marcou AccountsRequested: a próxima tentativa vai publicar.
    Assert.Equal(ProposalStatus.Approved, _store.Items[proposal.Id].Status);
  }

  private static ProposalApprovedEvent Approved(Guid proposalId) => new()
  {
    EventId = Guid.NewGuid(),
    CorrelationId = proposalId,
    ProposalId = proposalId,
    AnalysisId = Guid.NewGuid(),
    Version = ProposalApprovedEvent.ContractVersion,
    OccurredAt = DateTime.UtcNow,
    ApprovedBy = "backoffice"
  };
}