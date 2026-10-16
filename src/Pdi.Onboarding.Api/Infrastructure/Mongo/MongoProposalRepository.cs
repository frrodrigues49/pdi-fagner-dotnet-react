using MongoDB.Driver;
using Pdi.Onboarding.Api.Application.Proposals;
using Pdi.Onboarding.Api.Domain.Entities;
using Pdi.Onboarding.Api.Domain.Enums;

namespace Pdi.Onboarding.Api.Infrastructure.Mongo;

public sealed class MongoProposalRepository : IProposalRepository
{
  public const string CollectionName = "proposals";

  private readonly IMongoCollection<Proposal> _collection;

  public MongoProposalRepository(IMongoDatabase database)
  {
    _collection = database.GetCollection<Proposal>(CollectionName);
  }

  public Task InsertAsync(Proposal proposal, CancellationToken cancellationToken = default)
      => _collection.InsertOneAsync(proposal, cancellationToken: cancellationToken);

  public async Task<Proposal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
      => await _collection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

  public async Task<bool> TryReplaceAsync(
      Proposal proposal,
      ProposalStatus expectedStatus,
      int expectedVersion,
      CancellationToken cancellationToken = default)
  {
    var result = await _collection.ReplaceOneAsync(
        x => x.Id == proposal.Id
             && x.Status == expectedStatus
             && x.Version == expectedVersion,
        proposal,
        cancellationToken: cancellationToken);

    return result.ModifiedCount == 1;
  }
}