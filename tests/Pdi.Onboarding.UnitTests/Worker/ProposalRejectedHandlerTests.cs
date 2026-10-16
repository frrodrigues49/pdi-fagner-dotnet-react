using Microsoft.Extensions.Logging.Abstractions;
using Pdi.Onboarding.Contracts;
using Pdi.Onboarding.UnitTests.Fakes;
using Pdi.Onboarding.Worker.Consumers.ProposalRejected;
using Pdi.Onboarding.Worker.Kafka;
using Pdi.Onboarding.Worker.Proposals;

namespace Pdi.Onboarding.UnitTests.Worker;

public sealed class ProposalRejectedHandlerTests
{
  private readonly FakeProposalStore _store = new();
  private readonly ProposalRejectedHandler _handler;

  public ProposalRejectedHandlerTests()
  {
    _handler = new ProposalRejectedHandler(_store, NullLogger<ProposalRejectedHandler>.Instance);
  }

  [Fact]
  public async Task HandleAsync_WhenFinalized_ShouldRejectWithReason()
  {
    var proposal = _store.Add(ProposalStatus.Finalized);
    var message = Rejected(proposal.Id);

    var outcome = await _handler.HandleAsync(message, CancellationToken.None);

    Assert.Equal(ConsumerOutcome.Commit, outcome);
    var stored = _store.Items[proposal.Id];
    Assert.Equal(ProposalStatus.Rejected, stored.Status);
    Assert.Equal("Documentos ilegiveis", stored.RejectionReason);
    Assert.Equal(message.AnalysisId, stored.AnalysisId);
    Assert.NotNull(stored.RejectedAt);
  }

  [Fact]
  public async Task HandleAsync_WhenAlreadyRejected_ShouldCommit()
  {
    var proposal = _store.Add(ProposalStatus.Rejected);

    var outcome = await _handler.HandleAsync(Rejected(proposal.Id), CancellationToken.None);

    Assert.Equal(ConsumerOutcome.Commit, outcome);
  }

  [Theory]
  [InlineData(ProposalStatus.Draft)]
  [InlineData(ProposalStatus.Approved)]
  [InlineData(ProposalStatus.AccountsRequested)]
  public async Task HandleAsync_WhenStatusCannotBeRejected_ShouldDeadLetter(ProposalStatus status)
  {
    var proposal = _store.Add(status);

    var outcome = await _handler.HandleAsync(Rejected(proposal.Id), CancellationToken.None);

    Assert.Equal(ConsumerOutcome.DeadLetter, outcome);
    Assert.Equal(status, _store.Items[proposal.Id].Status);
  }

  [Fact]
  public async Task HandleAsync_WhenProposalDoesNotExist_ShouldDeadLetter()
  {
    var outcome = await _handler.HandleAsync(Rejected(Guid.NewGuid()), CancellationToken.None);

    Assert.Equal(ConsumerOutcome.DeadLetter, outcome);
  }

  [Fact]
  public async Task HandleAsync_WhenProposalChangedConcurrently_ShouldRetry()
  {
    var proposal = _store.Add(ProposalStatus.Finalized);
    _store.SimulateConflict = true;

    var outcome = await _handler.HandleAsync(Rejected(proposal.Id), CancellationToken.None);

    Assert.Equal(ConsumerOutcome.Retry, outcome);
  }

  private static ProposalRejectedEvent Rejected(Guid proposalId) => new()
  {
    EventId = Guid.NewGuid(),
    CorrelationId = proposalId,
    ProposalId = proposalId,
    AnalysisId = Guid.NewGuid(),
    Version = ProposalRejectedEvent.ContractVersion,
    OccurredAt = DateTime.UtcNow,
    RejectedBy = "backoffice",
    Reason = "Documentos ilegiveis"
  };
}