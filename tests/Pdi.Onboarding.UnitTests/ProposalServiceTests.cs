using System.Text.Json;
using Pdi.Onboarding.Api.Application.Proposals;
using Pdi.Onboarding.Api.Domain.Entities;
using Pdi.Onboarding.Api.Domain.Enums;
using Pdi.Onboarding.Contracts;

namespace Pdi.Onboarding.UnitTests;

public sealed class ProposalServiceTests
{
  private readonly FakeProposalRepository _repository = new();
  private readonly ProposalService _service;

  public ProposalServiceTests()
  {
    _service = new ProposalService(new ProposalValidator(), _repository);
  }

  [Fact]
  public async Task CreateAsync_ShouldPersistDraftWithVersion1()
  {
    var result = await _service.CreateAsync(new ProposalDataRequest { FullName = " Ana Souza " });

    Assert.Equal(ProposalOperationStatus.Success, result.Status);
    var stored = _repository.Items[result.Data!.Id];
    Assert.Equal(ProposalStatus.Draft, stored.Status);
    Assert.Equal(1, stored.Version);
    Assert.Equal("Ana Souza", stored.FullName);
    Assert.Empty(stored.PendingEvents);
  }

  [Fact]
  public async Task UpdateAsync_WhenDraft_ShouldChangeOnlyProvidedFields()
  {
    var created = await _service.CreateAsync(new ProposalDataRequest { FullName = "Ana Souza" });

    var result = await _service.UpdateAsync(created.Data!.Id, new ProposalDataRequest { State = "sp" });

    Assert.Equal(ProposalOperationStatus.Success, result.Status);
    Assert.Equal("Ana Souza", result.Data!.FullName);
    Assert.Equal("SP", result.Data.State);
    Assert.Equal(2, result.Data.Version);
  }

  [Fact]
  public async Task UpdateAsync_WhenNotFound_ShouldReturnNotFound()
  {
    var result = await _service.UpdateAsync(Guid.NewGuid(), new ProposalDataRequest());

    Assert.Equal(ProposalOperationStatus.NotFound, result.Status);
  }

  [Fact]
  public async Task FinalizeAsync_WhenComplete_ShouldFinalizeAndQueueEventInOutbox()
  {
    var created = await _service.CreateAsync(CompleteRequest());

    var result = await _service.FinalizeAsync(created.Data!.Id);

    Assert.Equal(ProposalOperationStatus.Success, result.Status);
    var stored = _repository.Items[created.Data.Id];
    Assert.Equal(ProposalStatus.Finalized, stored.Status);

    // Status e evento gravados juntos, na mesma escrita.
    var pending = Assert.Single(stored.PendingEvents);
    Assert.Equal(Topics.ProposalFinalized, pending.Topic);
    Assert.Equal(created.Data.Id.ToString(), pending.Key);
    var payload = JsonSerializer.Deserialize<ProposalFinalizedEvent>(pending.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    Assert.Equal(pending.Id, payload.EventId);
    Assert.Equal(created.Data.Id, payload.ProposalId);
    Assert.Equal(created.Data.Id, payload.CorrelationId);
    Assert.Equal("52998224725", payload.TaxId);
  }

  [Fact]
  public async Task FinalizeAsync_WhenIncomplete_ShouldReturnErrorsAndNotPublish()
  {
    var created = await _service.CreateAsync(new ProposalDataRequest { FullName = "Ana Souza" });

    var result = await _service.FinalizeAsync(created.Data!.Id);

    Assert.Equal(ProposalOperationStatus.ValidationFailed, result.Status);
    Assert.NotEmpty(result.Errors);
    Assert.Equal(ProposalStatus.Draft, _repository.Items[created.Data.Id].Status);
    Assert.Empty(_repository.Items[created.Data.Id].PendingEvents);
  }

  [Fact]
  public async Task FinalizeAsync_WhenAlreadyFinalized_ShouldReturnInvalidStatus()
  {
    var created = await _service.CreateAsync(CompleteRequest());
    await _service.FinalizeAsync(created.Data!.Id);

    var result = await _service.FinalizeAsync(created.Data.Id);

    Assert.Equal(ProposalOperationStatus.InvalidStatus, result.Status);
    Assert.Single(_repository.Items[created.Data.Id].PendingEvents);
  }

  [Fact]
  public async Task FinalizeAsync_WhenVersionConflict_ShouldReturnConflict()
  {
    var created = await _service.CreateAsync(CompleteRequest());
    _repository.SimulateConflict = true;

    var result = await _service.FinalizeAsync(created.Data!.Id);

    // Nada foi gravado, então o evento também não existe no banco.
    Assert.Equal(ProposalOperationStatus.ConcurrencyConflict, result.Status);
  }

  private static ProposalDataRequest CompleteRequest() => new()
  {
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

  private sealed class FakeProposalRepository : IProposalRepository
  {
    public Dictionary<Guid, Proposal> Items { get; } = [];
    public bool SimulateConflict { get; set; }

    public Task InsertAsync(Proposal proposal, CancellationToken cancellationToken = default)
    {
      Items[proposal.Id] = proposal;
      return Task.CompletedTask;
    }

    public Task<Proposal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.GetValueOrDefault(id));

    public Task<bool> TryReplaceAsync(
        Proposal proposal,
        ProposalStatus expectedStatus,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
      if (SimulateConflict)
        return Task.FromResult(false);

      Items[proposal.Id] = proposal;
      return Task.FromResult(true);
    }
  }
}
