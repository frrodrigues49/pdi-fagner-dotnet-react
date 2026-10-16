using MongoDB.Driver;
using Pdi.Onboarding.Worker.Proposals;

namespace Pdi.Onboarding.Worker.Mongo;

public sealed class MongoProposalStore : IProposalStore
{
  public const string CollectionName = "proposals";

  private static readonly UpdateDefinitionBuilder<ProposalDocument> Update = Builders<ProposalDocument>.Update;

  private readonly IMongoCollection<ProposalDocument> _collection;

  public MongoProposalStore(IMongoDatabase database)
  {
    _collection = database.GetCollection<ProposalDocument>(CollectionName);
  }

  public async Task<ProposalDocument?> GetAsync(Guid id, CancellationToken cancellationToken)
      => await _collection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

  public Task<bool> TryApproveAsync(
      Guid id, Guid analysisId, string approvedBy, DateTime at, CancellationToken cancellationToken)
      => TryTransitionAsync(id, ProposalStatus.Finalized, Update
          .Set(x => x.Status, ProposalStatus.Approved)
          .Set(x => x.AnalysisId, analysisId)
          .Set(x => x.ApprovedBy, approvedBy)
          .Set(x => x.ApprovedAt, at)
          .Set(x => x.UpdatedAt, at), cancellationToken);

  public Task<bool> TryMarkAccountsRequestedAsync(Guid id, DateTime at, CancellationToken cancellationToken)
      => TryTransitionAsync(id, ProposalStatus.Approved, Update
          .Set(x => x.Status, ProposalStatus.AccountsRequested)
          .Set(x => x.AccountsRequestedAt, at)
          .Set(x => x.UpdatedAt, at), cancellationToken);

  public Task<bool> TryRejectAsync(
      Guid id, Guid analysisId, string reason, DateTime at, CancellationToken cancellationToken)
      => TryTransitionAsync(id, ProposalStatus.Finalized, Update
          .Set(x => x.Status, ProposalStatus.Rejected)
          .Set(x => x.AnalysisId, analysisId)
          .Set(x => x.RejectionReason, reason)
          .Set(x => x.RejectedAt, at)
          .Set(x => x.UpdatedAt, at), cancellationToken);

  // $set + $inc em vez de ReplaceOne: altera só esses campos e preserva o resto do
  // documento (dados do cliente, pendingEvents do outbox...), que o Worker nem conhece.
  // O $inc na versão mantém a concorrência otimista da API valendo.
  private async Task<bool> TryTransitionAsync(
      Guid id,
      ProposalStatus from,
      UpdateDefinition<ProposalDocument> update,
      CancellationToken cancellationToken)
  {
    var result = await _collection.UpdateOneAsync(
        x => x.Id == id && x.Status == from,
        update.Inc(x => x.Version, 1),
        cancellationToken: cancellationToken);

    return result.ModifiedCount == 1;
  }
}