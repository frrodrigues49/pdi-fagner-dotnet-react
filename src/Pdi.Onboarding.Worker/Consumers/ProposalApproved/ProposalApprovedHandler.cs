using Pdi.Onboarding.Contracts;
using Pdi.Onboarding.Worker.Kafka;
using Pdi.Onboarding.Worker.Proposals;

namespace Pdi.Onboarding.Worker.Consumers.ProposalApproved;

/// proposta-conta-aprovada → precadastro (Finalized → Approved) → criar-conta → AccountsRequested.
///
/// O status funciona como checkpoint: se o processo cair em qualquer ponto, o replay
/// da mesma mensagem continua de onde parou, em vez de começar do zero.
///   Finalized         → ainda não fez nada: aprova, publica, marca
///   Approved          → aprovou mas talvez não publicou: publica, marca
///   AccountsRequested → já fez tudo: só comita
public sealed class ProposalApprovedHandler : IMessageHandler<ProposalApprovedEvent>
{
  private readonly IProposalStore _proposals;
  private readonly IEventPublisher _publisher;
  private readonly ILogger<ProposalApprovedHandler> _logger;

  public ProposalApprovedHandler(
      IProposalStore proposals,
      IEventPublisher publisher,
      ILogger<ProposalApprovedHandler> logger)
  {
    _proposals = proposals;
    _publisher = publisher;
    _logger = logger;
  }

  public async Task<ConsumerOutcome> HandleAsync(ProposalApprovedEvent message, CancellationToken cancellationToken)
  {
    var proposal = await _proposals.GetAsync(message.ProposalId, cancellationToken);
    if (proposal is null)
    {
      _logger.LogError("Proposal {ProposalId} not found for approved analysis {AnalysisId}", message.ProposalId, message.AnalysisId);
      return ConsumerOutcome.DeadLetter;
    }

    if (proposal.Status == ProposalStatus.AccountsRequested)
    {
      _logger.LogInformation("Proposal {ProposalId} already requested accounts; replay ignored", proposal.Id);
      return ConsumerOutcome.Commit;
    }

    if (proposal.Status == ProposalStatus.Finalized)
    {
      if (!await _proposals.TryApproveAsync(proposal.Id, message.AnalysisId, message.ApprovedBy, DateTime.UtcNow, cancellationToken))
        return ConsumerOutcome.Retry; // mudou entre a leitura e a escrita: relê na próxima tentativa

      proposal.Status = ProposalStatus.Approved;
    }

    if (proposal.Status != ProposalStatus.Approved)
    {
      // Draft ou Rejected: aprovação de algo que não podia ser aprovado. Não adianta insistir.
      _logger.LogError("Proposal {ProposalId} cannot be approved from {Status}", proposal.Id, proposal.Status);
      return ConsumerOutcome.DeadLetter;
    }

    await _publisher.PublishAsync(Topics.CreateAccount, new CreateAccountEvent
    {
      EventId = Guid.NewGuid(),
      CorrelationId = message.CorrelationId,
      ProposalId = proposal.Id,
      Version = CreateAccountEvent.ContractVersion,
      OccurredAt = DateTime.UtcNow,
      FullName = proposal.FullName ?? string.Empty,
      TaxId = proposal.TaxId ?? string.Empty,
      ProductCodes = ProductCodes.All
    }, cancellationToken);

    // false aqui = um replay concorrente já marcou. O resultado é o mesmo, então segue.
    await _proposals.TryMarkAccountsRequestedAsync(proposal.Id, DateTime.UtcNow, cancellationToken);

    _logger.LogInformation(
        "Proposal {ProposalId} approved; criar-conta requested for products {Products}",
        proposal.Id,
        string.Join(",", ProductCodes.All));

    return ConsumerOutcome.Commit;
  }
}