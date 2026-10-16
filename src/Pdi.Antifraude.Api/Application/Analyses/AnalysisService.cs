using Pdi.Antifraude.Api.Application.Events;
using Pdi.Antifraude.Api.Domain.Entities;
using Pdi.Antifraude.Api.Domain.Enums;
using Pdi.Onboarding.Contracts;

namespace Pdi.Antifraude.Api.Application.Analyses;

public sealed class AnalysisService : IAnalysisService
{
  private readonly CreateAnalysisValidator _createValidator;
  private readonly RejectAnalysisValidator _rejectValidator;
  private readonly IAnalysisRepository _repository;
  private readonly ILogger<AnalysisService> _logger;

  public AnalysisService(
      CreateAnalysisValidator createValidator,
      RejectAnalysisValidator rejectValidator,
      IAnalysisRepository repository,
      ILogger<AnalysisService> logger)
  {
    _createValidator = createValidator;
    _rejectValidator = rejectValidator;
    _repository = repository;
    _logger = logger;
  }

  public async Task<AnalysisResult> CreateAsync(
      CreateAnalysisRequest request,
      CancellationToken cancellationToken = default)
  {
    var errors = _createValidator.Validate(request);
    if (errors.Count > 0)
      return AnalysisResult.Invalid(errors);

    var now = DateTime.UtcNow;
    var analysis = new Analysis
    {
      Id = Guid.NewGuid(),
      ProposalId = request.ProposalId,
      EventId = request.EventId,
      CorrelationId = request.CorrelationId,
      Status = AnalysisStatus.Pending,
      FullName = request.FullName,
      TaxId = request.TaxId,
      Email = request.Email,
      Phone = request.Phone,
      BirthDate = request.BirthDate,
      Street = request.Street,
      City = request.City,
      State = request.State,
      PostalCode = request.PostalCode,
      MotherName = request.MotherName,
      CreatedAt = now,
      UpdatedAt = now
    };

    if (!await _repository.TryInsertAsync(analysis, cancellationToken))
    {
      _logger.LogInformation(
          "Analysis for proposal {ProposalId} already exists; event {EventId} ignored",
          request.ProposalId,
          request.EventId);
      return AnalysisResult.AlreadyExists();
    }

    _logger.LogInformation(
        "Analysis {AnalysisId} created for proposal {ProposalId}",
        analysis.Id,
        analysis.ProposalId);

    return AnalysisResult.Created(AnalysisResponse.From(analysis));
  }

  public async Task<IReadOnlyList<AnalysisResponse>> ListAsync(
      AnalysisStatus? status,
      CancellationToken cancellationToken = default)
  {
    var analyses = await _repository.ListAsync(status, cancellationToken);
    return analyses.Select(AnalysisResponse.From).ToList();
  }

  public async Task<AnalysisResult> GetAsync(Guid id, CancellationToken cancellationToken = default)
  {
    var analysis = await _repository.GetByIdAsync(id, cancellationToken);
    return analysis is null
        ? AnalysisResult.NotFound()
        : AnalysisResult.Ok(AnalysisResponse.From(analysis));
  }

  public async Task<AnalysisResult> ApproveAsync(
      Guid id,
      string decidedBy,
      CancellationToken cancellationToken = default)
  {
    var analysis = await _repository.GetByIdAsync(id, cancellationToken);
    if (analysis is null)
      return AnalysisResult.NotFound();

    if (analysis.Status != AnalysisStatus.Pending)
      return AnalysisResult.WrongStatus($"Analysis {id} cannot be approved from {analysis.Status}.");

    var now = DateTime.UtcNow;
    analysis.Status = AnalysisStatus.Approved;
    analysis.DecidedBy = decidedBy;
    analysis.DecidedAt = now;
    analysis.UpdatedAt = now;
    analysis.PendingEvents.Add(Outbox.Message(Topics.ProposalApproved, new ProposalApprovedEvent
    {
      EventId = Guid.NewGuid(),
      CorrelationId = analysis.CorrelationId,
      ProposalId = analysis.ProposalId,
      AnalysisId = analysis.Id,
      Version = ProposalApprovedEvent.ContractVersion,
      OccurredAt = now,
      ApprovedBy = decidedBy
    }));

    // Decisão + evento na mesma escrita (outbox): se gravou, o evento VAI sair.
    if (!await _repository.TryReplaceAsync(analysis, AnalysisStatus.Pending, cancellationToken))
      return AnalysisResult.WrongStatus($"Analysis {id} was decided by another request.");

    _logger.LogInformation(
        "Analysis {AnalysisId} approved for proposal {ProposalId}",
        analysis.Id,
        analysis.ProposalId);

    return AnalysisResult.Ok(AnalysisResponse.From(analysis));
  }

  public async Task<AnalysisResult> RejectAsync(
      Guid id,
      RejectAnalysisRequest request,
      string decidedBy,
      CancellationToken cancellationToken = default)
  {
    var errors = _rejectValidator.Validate(request);
    if (errors.Count > 0)
      return AnalysisResult.Invalid(errors);

    var analysis = await _repository.GetByIdAsync(id, cancellationToken);
    if (analysis is null)
      return AnalysisResult.NotFound();

    if (analysis.Status != AnalysisStatus.Pending)
      return AnalysisResult.WrongStatus($"Analysis {id} cannot be rejected from {analysis.Status}.");

    var now = DateTime.UtcNow;
    analysis.Status = AnalysisStatus.Rejected;
    var reason = request.Reason.Trim();
    analysis.RejectionReason = reason;
    analysis.DecidedBy = decidedBy;
    analysis.DecidedAt = now;
    analysis.UpdatedAt = now;
    analysis.PendingEvents.Add(Outbox.Message(Topics.ProposalRejected, new ProposalRejectedEvent
    {
      EventId = Guid.NewGuid(),
      CorrelationId = analysis.CorrelationId,
      ProposalId = analysis.ProposalId,
      AnalysisId = analysis.Id,
      Version = ProposalRejectedEvent.ContractVersion,
      OccurredAt = now,
      RejectedBy = decidedBy,
      Reason = reason
    }));

    if (!await _repository.TryReplaceAsync(analysis, AnalysisStatus.Pending, cancellationToken))
      return AnalysisResult.WrongStatus($"Analysis {id} was decided by another request.");

    _logger.LogInformation(
        "Analysis {AnalysisId} rejected for proposal {ProposalId}",
        analysis.Id,
        analysis.ProposalId);

    return AnalysisResult.Ok(AnalysisResponse.From(analysis));
  }
}