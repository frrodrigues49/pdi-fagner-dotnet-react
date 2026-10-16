using MongoDB.Driver;
using Pdi.Onboarding.Api.Application.Events;
using Pdi.Onboarding.Api.Domain.Entities;

namespace Pdi.Onboarding.Api.Infrastructure.Mongo;

public sealed class MongoProposalOutboxStore : IOutboxStore
{
  private readonly IMongoCollection<Proposal> _collection;

  public MongoProposalOutboxStore(IMongoDatabase database)
  {
    _collection = database.GetCollection<Proposal>(MongoProposalRepository.CollectionName);
  }

  public async Task<IReadOnlyList<PendingOutboxMessage>> GetPendingAsync(
      int limit,
      CancellationToken cancellationToken = default)
  {
    var documents = await _collection
        .Find(Builders<Proposal>.Filter.SizeGt(x => x.PendingEvents, 0))
        .Limit(limit)
        .ToListAsync(cancellationToken);

    return documents
        .SelectMany(d => d.PendingEvents.Select(m => new PendingOutboxMessage(d.Id, m)))
        .ToList();
  }

  // $pull remove só aquele evento, sem mexer em status/versão do documento.
  public Task MarkPublishedAsync(Guid documentId, Guid messageId, CancellationToken cancellationToken = default)
      => _collection.UpdateOneAsync(
          x => x.Id == documentId,
          Builders<Proposal>.Update.PullFilter(x => x.PendingEvents, m => m.Id == messageId),
          cancellationToken: cancellationToken);
}
