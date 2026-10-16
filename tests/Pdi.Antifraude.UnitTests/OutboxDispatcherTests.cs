using Microsoft.Extensions.Logging.Abstractions;
using Pdi.Antifraude.Api.Application.Events;
using Pdi.Antifraude.Api.Domain.Entities;
using Pdi.Antifraude.Api.Infrastructure.Messaging;
using Pdi.Antifraude.UnitTests.Fakes;

namespace Pdi.Antifraude.UnitTests;

public sealed class OutboxDispatcherTests
{
  private readonly FakeOutboxStore _store = new();
  private readonly FakeEventPublisher _publisher = new();
  private readonly OutboxDispatcher _dispatcher;

  public OutboxDispatcherTests()
  {
    _dispatcher = new OutboxDispatcher(_store, _publisher, NullLogger<OutboxDispatcher>.Instance);
  }

  [Fact]
  public async Task DispatchOnceAsync_ShouldPublishInOrderAndMarkAsPublished()
  {
    var documentId = Guid.NewGuid();
    var older = Message("proposta-conta-aprovada", DateTime.UtcNow.AddSeconds(-5));
    var newer = Message("proposta-conta-aprovada", DateTime.UtcNow);
    _store.Pending.Add(new PendingOutboxMessage(documentId, newer));
    _store.Pending.Add(new PendingOutboxMessage(documentId, older));

    var count = await _dispatcher.DispatchOnceAsync(CancellationToken.None);

    Assert.Equal(2, count);
    Assert.Equal([older.Payload, newer.Payload], _publisher.Events.Select(e => e.Payload));
    Assert.Equal([older.Id, newer.Id], _store.Published.Select(p => p.MessageId));
  }

  [Fact]
  public async Task DispatchOnceAsync_WhenKafkaFails_ShouldKeepMessagePending()
  {
    _store.Pending.Add(new PendingOutboxMessage(Guid.NewGuid(), Message("t", DateTime.UtcNow)));
    _publisher.Fail = true;

    await Assert.ThrowsAsync<InvalidOperationException>(
        () => _dispatcher.DispatchOnceAsync(CancellationToken.None));

    // Não marcou como publicado: na próxima rodada ele tenta de novo.
    Assert.Empty(_store.Published);
  }

  [Fact]
  public async Task DispatchOnceAsync_WhenNothingIsPending_ShouldReturnZero()
  {
    var count = await _dispatcher.DispatchOnceAsync(CancellationToken.None);

    Assert.Equal(0, count);
    Assert.Empty(_publisher.Events);
  }

  private static OutboxMessage Message(string topic, DateTime createdAt) => new()
  {
    Id = Guid.NewGuid(),
    Topic = topic,
    Key = Guid.NewGuid().ToString(),
    Payload = $"{{\"id\":\"{Guid.NewGuid()}\"}}",
    CreatedAt = createdAt
  };

  private sealed class FakeOutboxStore : IOutboxStore
  {
    public List<PendingOutboxMessage> Pending { get; } = [];
    public List<(Guid DocumentId, Guid MessageId)> Published { get; } = [];

    public Task<IReadOnlyList<PendingOutboxMessage>> GetPendingAsync(int limit, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PendingOutboxMessage>>(Pending.Take(limit).ToList());

    public Task MarkPublishedAsync(Guid documentId, Guid messageId, CancellationToken cancellationToken = default)
    {
      Published.Add((documentId, messageId));
      Pending.RemoveAll(p => p.Message.Id == messageId);
      return Task.CompletedTask;
    }
  }
}
