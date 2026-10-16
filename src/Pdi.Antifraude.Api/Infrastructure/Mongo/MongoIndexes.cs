using MongoDB.Driver;
using Pdi.Antifraude.Api.Domain.Entities;

namespace Pdi.Antifraude.Api.Infrastructure.Mongo;

public static class MongoIndexes
{
  // CreateIndex é idempotente: rodar em todo startup é seguro.
  // Precisa rodar depois de MongoConventions.Register() para o campo sair em camelCase.
  public static Task EnsureAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
  {
    var analyses = database.GetCollection<Analysis>(MongoAnalysisRepository.CollectionName);

    return analyses.Indexes.CreateManyAsync(
    [
      new CreateIndexModel<Analysis>(
          Builders<Analysis>.IndexKeys.Ascending(x => x.ProposalId),
          new CreateIndexOptions { Unique = true, Name = "ux_proposalId" }),
      new CreateIndexModel<Analysis>(
          Builders<Analysis>.IndexKeys.Ascending(x => x.Status).Ascending(x => x.CreatedAt),
          new CreateIndexOptions { Name = "ix_status_createdAt" })
    ], cancellationToken);
  }
}
