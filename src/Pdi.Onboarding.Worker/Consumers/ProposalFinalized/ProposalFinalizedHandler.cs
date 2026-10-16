using System.Net;
using Pdi.Onboarding.Contracts;
using Pdi.Onboarding.Worker.Antifraude;
using Pdi.Onboarding.Worker.Kafka;

namespace Pdi.Onboarding.Worker.Consumers.ProposalFinalized;

public sealed class ProposalFinalizedHandler : IMessageHandler<ProposalFinalizedEvent>
{
  private readonly AntifraudeApiClient _antifraude;
  private readonly ILogger<ProposalFinalizedHandler> _logger;

  public ProposalFinalizedHandler(
      AntifraudeApiClient antifraude,
      ILogger<ProposalFinalizedHandler> logger)
  {
    _antifraude = antifraude;
    _logger = logger;
  }

  public async Task<ConsumerOutcome> HandleAsync(
      ProposalFinalizedEvent message,
      CancellationToken cancellationToken)
  {
    var statusCode = await _antifraude.CreateAnalysisAsync(message, cancellationToken);
    var outcome = AnalysisCommitPolicy.Decide(statusCode);

    if (statusCode == HttpStatusCode.BadRequest)
      _logger.LogError(
          "Antifraude rejected event {EventId} for proposal {ProposalId}; sending to DLQ",
          message.EventId,
          message.ProposalId);
    else
      _logger.LogInformation(
          "Antifraude answered {StatusCode} for proposal {ProposalId} (correlation {CorrelationId}) -> {Outcome}",
          (int)statusCode,
          message.ProposalId,
          message.CorrelationId,
          outcome);

    return outcome;
  }
}