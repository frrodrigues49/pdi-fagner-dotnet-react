namespace Pdi.Onboarding.Api.Application.Proposals;

public interface IProposalService
{
  Task<ProposalResult> CreateAsync(ProposalDataRequest request, CancellationToken cancellationToken = default);
  Task<ProposalResult> UpdateAsync(Guid id, ProposalDataRequest request, CancellationToken cancellationToken = default);
  Task<ProposalResult> GetAsync(Guid id, CancellationToken cancellationToken = default);
  Task<ProposalResult> FinalizeAsync(Guid id, CancellationToken cancellationToken = default);
}