using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Pdi.Onboarding.Contracts;
using Pdi.Onboarding.UnitTests.Fakes;
using Pdi.Onboarding.Worker.Antifraude;
using Pdi.Onboarding.Worker.Consumers.ProposalFinalized;
using Pdi.Onboarding.Worker.Kafka;

namespace Pdi.Onboarding.UnitTests.Worker;

public sealed class ProposalFinalizedHandlerTests
{
  [Fact]
  public async Task HandleAsync_WhenAnalysisIsCreated_ShouldPostEventAndCommit()
  {
    var stub = new StubHttpMessageHandler(HttpStatusCode.Created);
    var message = FinalizedEvent();

    var outcome = await CreateHandler(stub).HandleAsync(message, CancellationToken.None);

    Assert.Equal(ConsumerOutcome.Commit, outcome);
    Assert.Equal(HttpMethod.Post, stub.LastRequest!.Method);
    Assert.Equal("/api/v1/analyses", stub.LastRequest.RequestUri!.AbsolutePath);

    using var body = JsonDocument.Parse(stub.LastBody!);
    Assert.Equal(message.ProposalId, body.RootElement.GetProperty("proposalId").GetGuid());
    Assert.Equal(message.EventId, body.RootElement.GetProperty("eventId").GetGuid());
    Assert.Equal("52998224725", body.RootElement.GetProperty("taxId").GetString());
  }

  [Theory]
  [InlineData(HttpStatusCode.NoContent, ConsumerOutcome.Commit)]
  [InlineData(HttpStatusCode.BadRequest, ConsumerOutcome.DeadLetter)]
  [InlineData(HttpStatusCode.InternalServerError, ConsumerOutcome.Retry)]
  public async Task HandleAsync_ShouldFollowCommitPolicy(HttpStatusCode statusCode, ConsumerOutcome expected)
  {
    var outcome = await CreateHandler(new StubHttpMessageHandler(statusCode))
        .HandleAsync(FinalizedEvent(), CancellationToken.None);

    Assert.Equal(expected, outcome);
  }

  [Fact]
  public async Task HandleAsync_WhenApiIsDown_ShouldLetExceptionBubbleForRetry()
  {
    var stub = new StubHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));

    await Assert.ThrowsAsync<HttpRequestException>(
        () => CreateHandler(stub).HandleAsync(FinalizedEvent(), CancellationToken.None));
  }

  private static ProposalFinalizedHandler CreateHandler(StubHttpMessageHandler stub)
  {
    var http = new HttpClient(stub) { BaseAddress = new Uri("http://antifraude.test") };
    return new ProposalFinalizedHandler(
        new AntifraudeApiClient(http),
        NullLogger<ProposalFinalizedHandler>.Instance);
  }

  private static ProposalFinalizedEvent FinalizedEvent()
  {
    var proposalId = Guid.NewGuid();
    return new ProposalFinalizedEvent
    {
      EventId = Guid.NewGuid(),
      CorrelationId = proposalId,
      ProposalId = proposalId,
      Version = ProposalFinalizedEvent.ContractVersion,
      OccurredAt = DateTime.UtcNow,
      FullName = "Ana Souza",
      TaxId = "52998224725",
      Email = "ana@email.com",
      Phone = "11999998888",
      BirthDate = new DateOnly(1990, 5, 15),
      Street = "Rua A, 100",
      City = "Sao Paulo",
      State = "SP",
      PostalCode = "01310100",
      MotherName = "Maria Souza"
    };
  }
}