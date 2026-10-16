using MongoDB.Bson;
using MongoDB.Driver;
using Pdi.IntegrationTests.Support;
using Pdi.Onboarding.Worker.Mongo;
using Pdi.Onboarding.Worker.Proposals;

namespace Pdi.IntegrationTests.Worker;

/// O Worker escreve no documento que a API Onboarding criou: $set parcial e condicional.
public sealed class MongoProposalStoreTests : IDisposable
{
  private readonly string _database = Infra.Unique("it-worker-precadastro");
  private readonly IMongoDatabase _db;
  private readonly MongoProposalStore _store;

  public MongoProposalStoreTests()
  {
    MongoConventions.Register();
    _db = new MongoClient(Infra.Mongo).GetDatabase(_database);
    _store = new MongoProposalStore(_db);
  }

  [InfraFact]
  public async Task Approve_ThenMarkAccountsRequested_ShouldBeConditionalAndKeepOtherFields()
  {
    var id = await InsertProposalAsTheApiWould("Finalized");
    var analysisId = Guid.NewGuid();
    var now = DateTime.UtcNow;

    Assert.True(await _store.TryApproveAsync(id, analysisId, "analista", now, CancellationToken.None));
    Assert.False(await _store.TryApproveAsync(id, analysisId, "analista", now, CancellationToken.None)); // já saiu de Finalized
    Assert.True(await _store.TryMarkAccountsRequestedAsync(id, now, CancellationToken.None));
    Assert.False(await _store.TryRejectAsync(id, analysisId, "tarde demais", now, CancellationToken.None));

    var typed = await _store.GetAsync(id, CancellationToken.None);
    Assert.Equal(ProposalStatus.AccountsRequested, typed!.Status);
    Assert.Equal(analysisId, typed.AnalysisId);
    Assert.Equal("analista", typed.ApprovedBy);

    var raw = await Raw(id);
    Assert.Equal("AccountsRequested", raw["status"].AsString); // enum como texto, igual à API
    Assert.Equal(4, raw["version"].AsInt32);                 // $inc: 2 → 3 → 4
    Assert.Equal("cliente@email.com", raw["email"].AsString);  // campo que o Worker nem conhece: preservado
    Assert.Single(raw["pendingEvents"].AsBsonArray);          // outbox da API: preservado
  }

  [InfraFact]
  public async Task Reject_ShouldStoreReason()
  {
    var id = await InsertProposalAsTheApiWould("Finalized");

    Assert.True(await _store.TryRejectAsync(id, Guid.NewGuid(), "Selfie divergente", DateTime.UtcNow, CancellationToken.None));

    var raw = await Raw(id);
    Assert.Equal("Rejected", raw["status"].AsString);
    Assert.Equal("Selfie divergente", raw["rejectionReason"].AsString);
  }

  [InfraFact]
  public async Task Get_WhenMissing_ShouldReturnNull()
  {
    Assert.Null(await _store.GetAsync(Guid.NewGuid(), CancellationToken.None));
  }

  private async Task<Guid> InsertProposalAsTheApiWould(string status)
  {
    var id = Guid.NewGuid();
    await Collection().InsertOneAsync(new BsonDocument
    {
      ["_id"] = new BsonBinaryData(id, GuidRepresentation.Standard),
      ["status"] = status,
      ["version"] = 2,
      ["fullName"] = "Cliente Integracao",
      ["taxId"] = "52998224725",
      ["email"] = "cliente@email.com",
      ["pendingEvents"] = new BsonArray { new BsonDocument("topic", "ainda-nao-publicado") }
    });
    return id;
  }

  private Task<BsonDocument> Raw(Guid id)
      => Collection().Find(Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(id, GuidRepresentation.Standard))).FirstAsync();

  private IMongoCollection<BsonDocument> Collection() => _db.GetCollection<BsonDocument>(MongoProposalStore.CollectionName);

  public void Dispose()
  {
    if (Infra.IsAvailable)
      _db.Client.DropDatabase(_database);
  }
}
