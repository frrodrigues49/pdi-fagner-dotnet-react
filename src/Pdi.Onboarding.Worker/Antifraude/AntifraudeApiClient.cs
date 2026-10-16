using System.Net;
using System.Net.Http.Json;
using Pdi.Onboarding.Contracts;
using Pdi.Onboarding.Worker.Kafka;

namespace Pdi.Onboarding.Worker.Antifraude;

public sealed class AntifraudeApiClient
{
  private readonly HttpClient _http;

  public AntifraudeApiClient(HttpClient http)
  {
    _http = http;
  }

  public async Task<HttpStatusCode> CreateAnalysisAsync(
      ProposalFinalizedEvent message,
      CancellationToken cancellationToken)
  {
    using var response = await _http.PostAsJsonAsync(
        "/api/v1/analyses",
        message,
        KafkaJson.Options,
        cancellationToken);

    return response.StatusCode;
  }
}