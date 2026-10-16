namespace Pdi.Onboarding.Worker.Antifraude;

public sealed class AntifraudeOptions
{
  public const string SectionName = "Antifraude";
  public const string InternalApiKeyHeader = "X-Internal-Api-Key";

  public string BaseUrl { get; set; } = "http://localhost:5004";
  public int TimeoutSeconds { get; set; } = 10;
  public string InternalApiKey { get; set; } = string.Empty;
}
