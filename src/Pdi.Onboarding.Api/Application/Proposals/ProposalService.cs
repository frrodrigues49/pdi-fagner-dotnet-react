using Pdi.Onboarding.Api.Application.Events;
using Pdi.Onboarding.Api.Domain.Entities;
using Pdi.Onboarding.Api.Domain.Enums;
using Pdi.Onboarding.Contracts;

namespace Pdi.Onboarding.Api.Application.Proposals;

public sealed class ProposalService : IProposalService
{
  private readonly ProposalValidator _validator;
  private readonly IProposalRepository _repository;

  public ProposalService(
      ProposalValidator validator,
      IProposalRepository repository)
  {
    _validator = validator;
    _repository = repository;
  }

  public async Task<ProposalResult> CreateAsync(
      ProposalDataRequest request,
      CancellationToken cancellationToken = default)
  {
    var now = DateTime.UtcNow;
    var proposal = new Proposal
    {
      Id = Guid.NewGuid(),
      Status = ProposalStatus.Draft,
      Version = 1,
      CreatedAt = now,
      UpdatedAt = now
    };
    Apply(proposal, request);

    await _repository.InsertAsync(proposal, cancellationToken);

    return ProposalResult.Ok(ProposalResponse.From(proposal));
  }

  public async Task<ProposalResult> UpdateAsync(
      Guid id,
      ProposalDataRequest request,
      CancellationToken cancellationToken = default)
  {
    var proposal = await _repository.GetByIdAsync(id, cancellationToken);
    if (proposal is null)
      return ProposalResult.NotFound();

    if (proposal.Status != ProposalStatus.Draft)
      return ProposalResult.WrongStatus($"Proposal {id} cannot be changed from {proposal.Status}.");

    var expectedVersion = proposal.Version;
    Apply(proposal, request);
    proposal.Version++;
    proposal.UpdatedAt = DateTime.UtcNow;

    var saved = await _repository.TryReplaceAsync(
        proposal, ProposalStatus.Draft, expectedVersion, cancellationToken);
    if (!saved)
      return ProposalResult.Conflict($"Proposal {id} was changed by another request.");

    return ProposalResult.Ok(ProposalResponse.From(proposal));
  }

  public async Task<ProposalResult> GetAsync(Guid id, CancellationToken cancellationToken = default)
  {
    var proposal = await _repository.GetByIdAsync(id, cancellationToken);
    return proposal is null
        ? ProposalResult.NotFound()
        : ProposalResult.Ok(ProposalResponse.From(proposal));
  }

  public async Task<ProposalResult> FinalizeAsync(Guid id, CancellationToken cancellationToken = default)
  {
    var proposal = await _repository.GetByIdAsync(id, cancellationToken);
    if (proposal is null)
      return ProposalResult.NotFound();

    if (proposal.Status != ProposalStatus.Draft)
      return ProposalResult.WrongStatus($"Proposal {id} cannot be finalized from {proposal.Status}.");

    var errors = _validator.Validate(ToValidationRequest(proposal));
    if (errors.Count > 0)
      return ProposalResult.Invalid(errors);

    var now = DateTime.UtcNow;
    var expectedVersion = proposal.Version;
    proposal.Status = ProposalStatus.Finalized;
    proposal.Version++;
    proposal.UpdatedAt = now;
    proposal.FinalizedAt = now;

    var finalized = new ProposalFinalizedEvent
    {
      EventId = Guid.NewGuid(),
      CorrelationId = proposal.Id,
      ProposalId = proposal.Id,
      Version = ProposalFinalizedEvent.ContractVersion,
      OccurredAt = now,
      FullName = proposal.FullName!,
      TaxId = proposal.TaxId!,
      Email = proposal.Email!,
      Phone = proposal.Phone!,
      BirthDate = proposal.BirthDate!.Value,
      Street = proposal.Street!,
      City = proposal.City!,
      State = proposal.State!,
      PostalCode = proposal.PostalCode!,
      MotherName = proposal.MotherName!
    };

    // Outbox: status novo + evento a publicar vão na MESMA escrita. Ou os dois são gravados,
    // ou nenhum. Quem publica no Kafka é o OutboxDispatcher, em background.
    proposal.PendingEvents.Add(Outbox.Message(Topics.ProposalFinalized, finalized));

    var saved = await _repository.TryReplaceAsync(
        proposal, ProposalStatus.Draft, expectedVersion, cancellationToken);
    if (!saved)
      return ProposalResult.Conflict($"Proposal {id} was changed by another request.");

    return ProposalResult.Ok(ProposalResponse.From(proposal));
  }

  private static void Apply(Proposal proposal, ProposalDataRequest request)
  {
    proposal.FullName = request.FullName?.Trim() ?? proposal.FullName;
    proposal.TaxId = request.TaxId?.Trim() ?? proposal.TaxId;
    proposal.Email = request.Email?.Trim() ?? proposal.Email;
    proposal.Phone = request.Phone?.Trim() ?? proposal.Phone;
    proposal.BirthDate = request.BirthDate ?? proposal.BirthDate;
    proposal.Street = request.Street?.Trim() ?? proposal.Street;
    proposal.City = request.City?.Trim() ?? proposal.City;
    proposal.State = request.State?.Trim().ToUpperInvariant() ?? proposal.State;
    proposal.PostalCode = request.PostalCode?.Trim() ?? proposal.PostalCode;
    proposal.MotherName = request.MotherName?.Trim() ?? proposal.MotherName;
  }

  private static ProposalValidationRequest ToValidationRequest(Proposal proposal) => new()
  {
    FullName = proposal.FullName ?? string.Empty,
    TaxId = proposal.TaxId ?? string.Empty,
    Email = proposal.Email ?? string.Empty,
    Phone = proposal.Phone ?? string.Empty,
    BirthDate = proposal.BirthDate ?? default,
    Street = proposal.Street ?? string.Empty,
    City = proposal.City ?? string.Empty,
    State = proposal.State ?? string.Empty,
    PostalCode = proposal.PostalCode ?? string.Empty,
    MotherName = proposal.MotherName ?? string.Empty
  };
}