using Pdi.Onboarding.Contracts;
using Pdi.Onboarding.Worker.Kafka;
using Pdi.Onboarding.Worker.Proposals;

namespace Pdi.Onboarding.Worker.Consumers.ProposalRejected;

/// proposta-conta-rejeitada → precadastro (Finalized → Rejected). Não cria conta.
public sealed class ProposalRejectedHandler : IMessageHandler<ProposalRejectedEvent>
{
  private readonly IProposalStore _proposals;
  private readonly ILogger<ProposalRejectedHandler> _logger;

  public ProposalRejectedHandler(IProposalStore proposals, ILogger<ProposalRejectedHandler> logger)
  {
    _proposals = proposals;
    _logger = logger;
  }

  public async Task<ConsumerOutcome> HandleAsync(ProposalRejectedEvent message, CancellationToken cancellationToken)
  {
    var proposal = await _proposals.GetAsync(message.ProposalId, cancellationToken);
    if (proposal is null)
    {
      _logger.LogError("Proposal {ProposalId} not found for rejected analysis {AnalysisId}", message.ProposalId, message.AnalysisId);
      return ConsumerOutcome.DeadLetter;
    }

    if (proposal.Status == ProposalStatus.Rejected)
    {
      _logger.LogInformation("Proposal {ProposalId} already rejected; replay ignored", proposal.Id);
      return ConsumerOutcome.Commit;
    }

    if (proposal.Status != ProposalStatus.Finalized)
    {
      _logger.LogError("Proposal {ProposalId} cannot be rejected from {Status}", proposal.Id, proposal.Status);
      return ConsumerOutcome.DeadLetter;
    }

    if (!await _proposals.TryRejectAsync(proposal.Id, message.AnalysisId, message.Reason, DateTime.UtcNow, cancellationToken))
      return ConsumerOutcome.Retry;

    _logger.LogInformation("Proposal {ProposalId} rejected: {Reason}", proposal.Id, message.Reason);
    return ConsumerOutcome.Commit;
  }
}
