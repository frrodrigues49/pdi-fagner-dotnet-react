namespace Pdi.Onboarding.Worker.Kafka;

public static class RetryBackoff
{
  public static readonly TimeSpan Max = TimeSpan.FromSeconds(30);

  /// Exponencial: 1s, 2s, 4s, 8s, 16s, 30s, 30s...
  public static TimeSpan For(int attempt)
  {
    var seconds = Math.Pow(2, Math.Max(attempt, 1) - 1);
    return TimeSpan.FromSeconds(Math.Min(seconds, Max.TotalSeconds));
  }
}