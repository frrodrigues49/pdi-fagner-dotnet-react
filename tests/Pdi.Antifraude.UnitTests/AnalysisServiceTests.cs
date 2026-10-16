using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Pdi.Antifraude.Api.Application.Analyses;
using Pdi.Antifraude.Api.Domain.Entities;
using Pdi.Antifraude.Api.Domain.Enums;
using Pdi.Antifraude.UnitTests.Fakes;
using Pdi.Onboarding.Contracts;

namespace Pdi.Antifraude.UnitTests;

public sealed class AnalysisServiceTests
{
  private readonly FakeAnalysisRepository _repository = new();
  private readonly AnalysisService _service;

  public AnalysisServiceTests()
  {
    _service = new AnalysisService(
        new CreateAnalysisValidator(),
        new RejectAnalysisValidator(),
        _repository,
        NullLogger<AnalysisService>.Instance);
  }

  [Fact]
  public async Task CreateAsync_WhenProposalIsNew_ShouldCreatePendingAnalysis()
  {
    var request = ValidRequest();

    var result = await _service.CreateAsync(request);

    Assert.Equal(AnalysisOperationStatus.Created, result.Status);
    var stored = Assert.Single(_repository.Items.Values);
    Assert.Equal(AnalysisStatus.Pending, stored.Status);
    Assert.Equal(request.ProposalId, stored.ProposalId);
    Assert.Equal(request.TaxId, stored.TaxId);
    Assert.Empty(stored.PendingEvents);
  }

  [Fact]
  public async Task CreateAsync_WhenProposalAlreadyHasAnalysis_ShouldReturnAlreadyExists()
  {
    var request = ValidRequest();
    await _service.CreateAsync(request);

    var replay = await _service.CreateAsync(request);

    Assert.Equal(AnalysisOperationStatus.AlreadyExists, replay.Status);
    Assert.Single(_repository.Items);
  }

  [Fact]
  public async Task CreateAsync_WhenIdsAreEmpty_ShouldReturnValidationFailed()
  {
    var request = ValidRequest();
    request.EventId = Guid.Empty;
    request.ProposalId = Guid.Empty;

    var result = await _service.CreateAsync(request);

    Assert.Equal(AnalysisOperationStatus.ValidationFailed, result.Status);
    Assert.Equal(2, result.Errors.Count);
    Assert.Empty(_repository.Items);
  }

  [Fact]
  public async Task ListAsync_WhenStatusIsGiven_ShouldReturnOnlyThatStatus()
  {
    AddPendingAnalysis();
    var approved = AddPendingAnalysis();
    approved.Status = AnalysisStatus.Approved;

    var pending = await _service.ListAsync(AnalysisStatus.Pending);
    var all = await _service.ListAsync(null);

    Assert.Single(pending);
    Assert.Equal(2, all.Count);
  }

  [Fact]
  public async Task GetAsync_WhenMissing_ShouldReturnNotFound()
  {
    var result = await _service.GetAsync(Guid.NewGuid());

    Assert.Equal(AnalysisOperationStatus.NotFound, result.Status);
  }

  [Fact]
  public async Task ApproveAsync_WhenPending_ShouldApproveAndQueueEventInOutbox()
  {
    var analysis = AddPendingAnalysis();

    var result = await _service.ApproveAsync(analysis.Id);

    Assert.Equal(AnalysisOperationStatus.Success, result.Status);
    var stored = _repository.Items[analysis.Id];
    Assert.Equal(AnalysisStatus.Approved, stored.Status);
    Assert.NotNull(stored.DecidedAt);
    var pending = Assert.Single(stored.PendingEvents);
    Assert.Equal(Topics.ProposalApproved, pending.Topic);
    Assert.Equal(analysis.CorrelationId.ToString(), pending.Key);
    var approved = JsonSerializer.Deserialize<ProposalApprovedEvent>(pending.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    Assert.Equal(pending.Id, approved.EventId);
    Assert.Equal(analysis.ProposalId, approved.ProposalId);
    Assert.Equal(analysis.Id, approved.AnalysisId);
    Assert.Equal(analysis.CorrelationId, approved.CorrelationId);
  }

  [Fact]
  public async Task ApproveAsync_WhenMissing_ShouldReturnNotFound()
  {
    var result = await _service.ApproveAsync(Guid.NewGuid());

    Assert.Equal(AnalysisOperationStatus.NotFound, result.Status);
  }

  [Fact]
  public async Task ApproveAsync_WhenAlreadyRejected_ShouldReturnInvalidStatus()
  {
    var analysis = AddPendingAnalysis();
    analysis.Status = AnalysisStatus.Rejected;

    var result = await _service.ApproveAsync(analysis.Id);

    Assert.Equal(AnalysisOperationStatus.InvalidStatus, result.Status);
    Assert.Empty(analysis.PendingEvents);
  }

  [Fact]
  public async Task ApproveAsync_WhenDecidedConcurrently_ShouldReturnInvalidStatus()
  {
    var analysis = AddPendingAnalysis();
    _repository.SimulateConflict = true;

    var result = await _service.ApproveAsync(analysis.Id);

    // O ReplaceOne condicional não casou: nada gravado, logo nenhum evento no banco.
    Assert.Equal(AnalysisOperationStatus.InvalidStatus, result.Status);
  }

  [Fact]
  public async Task RejectAsync_WhenPending_ShouldRejectAndQueueRejectedEvent()
  {
    var analysis = AddPendingAnalysis();

    var result = await _service.RejectAsync(analysis.Id, new RejectAnalysisRequest { Reason = " Documentos ilegiveis " });

    Assert.Equal(AnalysisOperationStatus.Success, result.Status);
    var stored = _repository.Items[analysis.Id];
    Assert.Equal(AnalysisStatus.Rejected, stored.Status);
    Assert.Equal("Documentos ilegiveis", stored.RejectionReason);

    // Não aciona criar-conta: o único evento é o de rejeição.
    var pending = Assert.Single(stored.PendingEvents);
    Assert.Equal(Topics.ProposalRejected, pending.Topic);
    var rejected = JsonSerializer.Deserialize<ProposalRejectedEvent>(pending.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    Assert.Equal(analysis.ProposalId, rejected.ProposalId);
    Assert.Equal("Documentos ilegiveis", rejected.Reason);
  }

  [Fact]
  public async Task RejectAsync_WhenReasonIsTooShort_ShouldReturnValidationFailed()
  {
    var analysis = AddPendingAnalysis();

    var result = await _service.RejectAsync(analysis.Id, new RejectAnalysisRequest { Reason = "x" });

    Assert.Equal(AnalysisOperationStatus.ValidationFailed, result.Status);
    Assert.Equal(AnalysisStatus.Pending, analysis.Status);
  }

  [Fact]
  public async Task RejectAsync_WhenAlreadyApproved_ShouldReturnInvalidStatus()
  {
    var analysis = AddPendingAnalysis();
    analysis.Status = AnalysisStatus.Approved;

    var result = await _service.RejectAsync(analysis.Id, new RejectAnalysisRequest { Reason = "Documentos ilegiveis" });

    Assert.Equal(AnalysisOperationStatus.InvalidStatus, result.Status);
    Assert.Null(analysis.RejectionReason);
  }

  private Analysis AddPendingAnalysis()
  {
    var analysis = new Analysis
    {
      Id = Guid.NewGuid(),
      ProposalId = Guid.NewGuid(),
      EventId = Guid.NewGuid(),
      CorrelationId = Guid.NewGuid(),
      Status = AnalysisStatus.Pending,
      FullName = "Ana Souza",
      TaxId = "52998224725",
      CreatedAt = DateTime.UtcNow,
      UpdatedAt = DateTime.UtcNow
    };
    _repository.Items[analysis.Id] = analysis;
    return analysis;
  }

  private static CreateAnalysisRequest ValidRequest()
  {
    var proposalId = Guid.NewGuid();
    return new CreateAnalysisRequest
    {
      EventId = Guid.NewGuid(),
      CorrelationId = proposalId,
      ProposalId = proposalId,
      FullName = "Ana Souza",
      TaxId = "52998224725",
      Email = "ana@email.com",
      Phone = "11999998888",
      BirthDate = new DateOnly(1990, 5, 15),
      Street = "Rua A, 100",
      City = "Sao Paulo",
      State = "SP",
      PostalCode = "01310100",
      MotherName = "Maria Souza"
    };
  }
}
