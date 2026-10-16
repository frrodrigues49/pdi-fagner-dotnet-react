using System.Net;
using Pdi.Onboarding.Worker.Consumers.ProposalFinalized;
using Pdi.Onboarding.Worker.Kafka;

namespace Pdi.Onboarding.UnitTests.Worker;

public sealed class AnalysisCommitPolicyTests
{
  [Theory]
  [InlineData(HttpStatusCode.Created, ConsumerOutcome.Commit)]
  [InlineData(HttpStatusCode.NoContent, ConsumerOutcome.Commit)]
  [InlineData(HttpStatusCode.BadRequest, ConsumerOutcome.DeadLetter)]
  [InlineData(HttpStatusCode.Unauthorized, ConsumerOutcome.Retry)]
  [InlineData(HttpStatusCode.NotFound, ConsumerOutcome.Retry)]
  [InlineData(HttpStatusCode.TooManyRequests, ConsumerOutcome.Retry)]
  [InlineData(HttpStatusCode.InternalServerError, ConsumerOutcome.Retry)]
  [InlineData(HttpStatusCode.ServiceUnavailable, ConsumerOutcome.Retry)]
  public void Decide_ShouldMapStatusCodeToOutcome(HttpStatusCode statusCode, ConsumerOutcome expected)
  {
    Assert.Equal(expected, AnalysisCommitPolicy.Decide(statusCode));
  }
}
