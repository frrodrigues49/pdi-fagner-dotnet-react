using MongoDB.Driver;
using Pdi.Antifraude.Api.Application.Events;
using Pdi.Antifraude.Api.Domain.Entities;

namespace Pdi.Antifraude.Api.Infrastructure.Mongo;

public sealed class MongoAnalysisOutboxStore : IOutboxStore
{
  private readonly IMongoCollection<Analysis> _collection;

  public MongoAnalysisOutboxStore(IMongoDatabase database)
  {
    _collection = database.GetCollection<Analysis>(MongoAnalysisRepository.CollectionName);
  }

  public async Task<IReadOnlyList<PendingOutboxMessage>> GetPendingAsync(
      int limit,
      CancellationToken cancellationToken = default)
  {
    var documents = await _collection
        .Find(Builders<Analysis>.Filter.SizeGt(x => x.PendingEvents, 0))
        .Limit(limit)
        .ToListAsync(cancellationToken);

    return documents
        .SelectMany(d => d.PendingEvents.Select(m => new PendingOutboxMessage(d.Id, m)))
        .ToList();
  }

  // $pull remove só aquele evento, sem mexer no status da análise.
  public Task MarkPublishedAsync(Guid documentId, Guid messageId, CancellationToken cancellationToken = default)
      => _collection.UpdateOneAsync(
          x => x.Id == documentId,
          Builders<Analysis>.Update.PullFilter(x => x.PendingEvents, m => m.Id == messageId),
          cancellationToken: cancellationToken);
}
