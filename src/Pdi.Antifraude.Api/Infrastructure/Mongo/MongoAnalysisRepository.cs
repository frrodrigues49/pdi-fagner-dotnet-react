using MongoDB.Driver;
using Pdi.Antifraude.Api.Application.Analyses;
using Pdi.Antifraude.Api.Domain.Entities;
using Pdi.Antifraude.Api.Domain.Enums;

namespace Pdi.Antifraude.Api.Infrastructure.Mongo;

public sealed class MongoAnalysisRepository : IAnalysisRepository
{
  public const string CollectionName = "analyses";

  private readonly IMongoCollection<Analysis> _collection;

  public MongoAnalysisRepository(IMongoDatabase database)
  {
    _collection = database.GetCollection<Analysis>(CollectionName);
  }

  public async Task<bool> TryInsertAsync(Analysis analysis, CancellationToken cancellationToken = default)
  {
    try
    {
      await _collection.InsertOneAsync(analysis, cancellationToken: cancellationToken);
      return true;
    }
    catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
    {
      return false;
    }
  }

  public async Task<Analysis?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
      => await _collection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

  public async Task<IReadOnlyList<Analysis>> ListAsync(
      AnalysisStatus? status,
      CancellationToken cancellationToken = default)
  {
    var filter = status is null
        ? Builders<Analysis>.Filter.Empty
        : Builders<Analysis>.Filter.Eq(x => x.Status, status.Value);

    return await _collection.Find(filter)
        .SortBy(x => x.CreatedAt)
        .ToListAsync(cancellationToken);
  }

  public async Task<bool> TryReplaceAsync(
      Analysis analysis,
      AnalysisStatus expectedStatus,
      CancellationToken cancellationToken = default)
  {
    var result = await _collection.ReplaceOneAsync(
        x => x.Id == analysis.Id && x.Status == expectedStatus,
        analysis,
        cancellationToken: cancellationToken);

    return result.ModifiedCount == 1;
  }
}
