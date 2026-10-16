using Pdi.Onboarding.Worker.Kafka;

namespace Pdi.Onboarding.UnitTests.Worker;

public sealed class RetryBackoffTests
{
  [Theory]
  [InlineData(1, 1)]
  [InlineData(2, 2)]
  [InlineData(3, 4)]
  [InlineData(5, 16)]
  [InlineData(6, 30)]
  [InlineData(50, 30)]
  public void For_ShouldGrowExponentiallyUpToMax(int attempt, int expectedSeconds)
  {
    Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), RetryBackoff.For(attempt));
  }
}