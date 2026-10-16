using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Pdi.IntegrationTests.Support;
using Pdi.Onboarding.Contracts;

namespace Pdi.IntegrationTests.Apis;

/// API Antifraude de verdade: JWT, API key interna, Mongo real (índices, outbox).
public sealed class AntifraudeApiTests : IClassFixture<AntifraudeApiFactory>
{
  private readonly AntifraudeApiFactory _factory;
  private readonly HttpClient _http;

  public AntifraudeApiTests(AntifraudeApiFactory factory)
  {
    _factory = factory;
    _http = factory.CreateClient();
  }

  [InfraFact]
  public async Task Login_ShouldIssueTokenOnlyForValidCredentials()
  {
    var wrong = await _http.PostAsJsonAsync("/api/v1/auth/login", new { username = "analista", password = "errada" });
    Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

    var ok = await _http.PostAsJsonAsync("/api/v1/auth/login", new { username = "analista", password = "analista123" });
    Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    var body = await ok.Content.ReadFromJsonAsync<JsonElement>();
    Assert.Equal("analista", body.GetProperty("username").GetString());
    Assert.False(string.IsNullOrEmpty(body.GetProperty("accessToken").GetString()));
  }

  [InfraFact]
  public async Task ProtectedRoutes_ShouldRequireJwtOrInternalKey()
  {
    Assert.Equal(HttpStatusCode.Unauthorized, (await _http.GetAsync("/api/v1/analyses")).StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await _http.PostAsync($"/api/v1/analyses/{Guid.NewGuid()}/approve", null)).StatusCode);

    using var withoutKey = new HttpRequestMessage(HttpMethod.Post, "/api/v1/analyses") { Content = JsonContent.Create(NewAnalysis(Guid.NewGuid())) };
    Assert.Equal(HttpStatusCode.Unauthorized, (await _http.SendAsync(withoutKey)).StatusCode);

    using var wrongKey = InternalPost(NewAnalysis(Guid.NewGuid()), key: "errada");
    Assert.Equal(HttpStatusCode.Unauthorized, (await _http.SendAsync(wrongKey)).StatusCode);

    using var garbageToken = new HttpRequestMessage(HttpMethod.Get, "/api/v1/analyses");
    garbageToken.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "nao.e.um.jwt");
    Assert.Equal(HttpStatusCode.Unauthorized, (await _http.SendAsync(garbageToken)).StatusCode);
  }

  [InfraFact]
  public async Task Create_ShouldBeIdempotentByProposalThanksToUniqueIndex()
  {
    var proposalId = Guid.NewGuid();

    using var first = InternalPost(NewAnalysis(proposalId));
    var created = await _http.SendAsync(first);
    Assert.Equal(HttpStatusCode.Created, created.StatusCode);

    using var replay = InternalPost(NewAnalysis(proposalId));
    Assert.Equal(HttpStatusCode.NoContent, (await _http.SendAsync(replay)).StatusCode);

    using var invalid = InternalPost(new { fullName = "Sem ids" });
    Assert.Equal(HttpStatusCode.BadRequest, (await _http.SendAsync(invalid)).StatusCode);

    var indexes = await (await _factory.Mongo().GetCollection<BsonDocument>("analyses").Indexes.ListAsync()).ToListAsync();
    var unique = Assert.Single(indexes, i => i["name"] == "ux_proposalId");
    Assert.True(unique["unique"].AsBoolean);
    Assert.Equal(new BsonDocument("proposalId", 1), unique["key"].AsBsonDocument);
  }

  [InfraFact]
  public async Task Approve_ShouldRecordAuthenticatedUserAndPublishOnce()
  {
    await Authenticate("supervisor", "supervisor123");
    var id = await CreateAnalysis(out var proposalId);

    var pending = await _http.GetFromJsonAsync<JsonElement>("/api/v1/analyses?status=Pending");
    Assert.Contains(pending.EnumerateArray(), a => a.GetProperty("id").GetGuid() == id);

    var approved = await _http.PostAsync($"/api/v1/analyses/{id}/approve", null);
    Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
    var body = await approved.Content.ReadFromJsonAsync<JsonElement>();
    Assert.Equal("Approved", body.GetProperty("status").GetString());
    Assert.Equal("supervisor", body.GetProperty("decidedBy").GetString());

    Assert.Equal(HttpStatusCode.Conflict, (await _http.PostAsync($"/api/v1/analyses/{id}/approve", null)).StatusCode);

    var published = await Eventually.Get(
        () => Task.FromResult(_factory.Publisher.Events.FirstOrDefault(e => e.Key == proposalId.ToString() && e.Topic == Topics.ProposalApproved)),
        _ => true,
        because: "approval should leave the outbox");
    Assert.Equal("supervisor", JsonDocument.Parse(published.Payload).RootElement.GetProperty("approvedBy").GetString());
    await Task.Delay(1500);
    Assert.Single(_factory.Publisher.Events, e => e.Key == proposalId.ToString());

    var detail = await _http.GetFromJsonAsync<JsonElement>($"/api/v1/analyses/{id}");
    Assert.Equal("Approved", detail.GetProperty("status").GetString());
    Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/api/v1/analyses/{Guid.NewGuid()}")).StatusCode);
  }

  [InfraFact]
  public async Task Reject_ShouldRequireReasonAndPublishRejectedEvent()
  {
    await Authenticate("analista", "analista123");
    var id = await CreateAnalysis(out var proposalId);

    var tooShort = await _http.PostAsJsonAsync($"/api/v1/analyses/{id}/reject", new { reason = "x" });
    Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);

    var rejected = await _http.PostAsJsonAsync($"/api/v1/analyses/{id}/reject", new { reason = "Selfie divergente" });
    Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
    Assert.Equal("Selfie divergente", (await rejected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rejectionReason").GetString());

    Assert.Equal(HttpStatusCode.Conflict, (await _http.PostAsync($"/api/v1/analyses/{id}/approve", null)).StatusCode);

    var published = await Eventually.Get(
        () => Task.FromResult(_factory.Publisher.Events.FirstOrDefault(e => e.Key == proposalId.ToString())),
        _ => true);
    Assert.Equal(Topics.ProposalRejected, published.Topic);
    Assert.Equal("analista", JsonDocument.Parse(published.Payload).RootElement.GetProperty("rejectedBy").GetString());

    var rejectedList = await _http.GetFromJsonAsync<JsonElement>("/api/v1/analyses?status=Rejected");
    Assert.Contains(rejectedList.EnumerateArray(), a => a.GetProperty("id").GetGuid() == id);
  }

  private async Task Authenticate(string username, string password)
  {
    var login = await _http.PostAsJsonAsync("/api/v1/auth/login", new { username, password });
    login.EnsureSuccessStatusCode();
    var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
    _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
  }

  private Task<Guid> CreateAnalysis(out Guid proposalId)
  {
    var id = Guid.NewGuid();
    proposalId = id;
    return Send();

    async Task<Guid> Send()
    {
      using var request = InternalPost(NewAnalysis(id));
      var response = await _http.SendAsync(request);
      response.EnsureSuccessStatusCode();
      return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
  }

  private static HttpRequestMessage InternalPost(object body, string key = AntifraudeApiFactory.InternalKey)
  {
    var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/analyses") { Content = JsonContent.Create(body) };
    request.Headers.Add("X-Internal-Api-Key", key);
    return request;
  }

  private static object NewAnalysis(Guid proposalId) => new
  {
    eventId = Guid.NewGuid(),
    correlationId = proposalId,
    proposalId,
    fullName = "Integracao Antifraude",
    taxId = "52998224725",
    email = "it@email.com",
    birthDate = "1990-05-15"
  };
}
