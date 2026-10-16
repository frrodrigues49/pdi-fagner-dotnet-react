using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Pdi.IntegrationTests.Support;
using Pdi.Onboarding.Contracts;

namespace Pdi.IntegrationTests.Apis;

/// API Onboarding de verdade (Program, DI, controllers, JSON) contra o Mongo real.
public sealed class OnboardingApiTests : IClassFixture<OnboardingApiFactory>
{
  private readonly OnboardingApiFactory _factory;
  private readonly HttpClient _http;

  public OnboardingApiTests(OnboardingApiFactory factory)
  {
    _factory = factory;
    _http = factory.CreateClient();
  }

  [InfraFact]
  public async Task Draft_Patch_Finalize_ShouldPersistAndPublishThroughOutbox()
  {
    var created = await _http.PostAsJsonAsync("/api/v1/proposals", new { fullName = "Integracao Onboarding", taxId = "52998224725" });
    Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    var id = (await Json(created)).GetProperty("id").GetGuid();
    Assert.Equal($"/api/v1/proposals/{id}", created.Headers.Location!.OriginalString);

    var patched = await _http.PatchAsJsonAsync($"/api/v1/proposals/{id}", CompleteData(state: "sp"));
    Assert.Equal(HttpStatusCode.OK, patched.StatusCode);
    var patchedBody = await Json(patched);
    Assert.Equal("SP", patchedBody.GetProperty("state").GetString());
    Assert.Equal(2, patchedBody.GetProperty("version").GetInt32());

    var finalized = await _http.PostAsync($"/api/v1/proposals/{id}/finalize", null);
    Assert.Equal(HttpStatusCode.OK, finalized.StatusCode);
    Assert.Equal("Finalized", (await Json(finalized)).GetProperty("status").GetString());

    // O OutboxDispatcher (hosted service real) publica e remove o evento do documento.
    var published = await Eventually.Get(
        () => Task.FromResult(_factory.Publisher.Events.FirstOrDefault(e => e.Key == id.ToString())),
        e => true,
        because: "OutboxDispatcher should publish the finalized event");
    Assert.Equal(Topics.ProposalFinalized, published.Topic);
    var payload = JsonDocument.Parse(published.Payload).RootElement;
    Assert.Equal(id, payload.GetProperty("proposalId").GetGuid());
    Assert.Equal("52998224725", payload.GetProperty("taxId").GetString());

    await Eventually.True(async () => (await PendingEvents(id)) == 0, because: "outbox should be drained");

    var fetched = await _http.GetFromJsonAsync<JsonElement>($"/api/v1/proposals/{id}");
    Assert.Equal("Finalized", fetched.GetProperty("status").GetString());
    Assert.Equal(JsonValueKind.Null, fetched.GetProperty("approvedBy").ValueKind);
  }

  [InfraFact]
  public async Task Finalize_WhenDataIsInvalid_ShouldReturn400WithAllErrorsAndStayDraft()
  {
    var id = await CreateDraft(new { fullName = "X", taxId = "11111111111" });

    var response = await _http.PostAsync($"/api/v1/proposals/{id}/finalize", null);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var errors = (await Json(response)).GetProperty("errors").EnumerateArray().Select(e => e.GetString()).ToList();
    Assert.Contains(errors, e => e!.Contains("TaxId"));
    Assert.Contains(errors, e => e!.Contains("FullName"));
    Assert.True(errors.Count >= 5);
    Assert.Equal("Draft", (await _http.GetFromJsonAsync<JsonElement>($"/api/v1/proposals/{id}")).GetProperty("status").GetString());
  }

  [InfraFact]
  public async Task InvalidTransitions_ShouldReturn404Or409()
  {
    Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/api/v1/proposals/{Guid.NewGuid()}")).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await _http.PostAsync($"/api/v1/proposals/{Guid.NewGuid()}/finalize", null)).StatusCode);

    var id = await CreateDraft(CompleteData());
    Assert.Equal(HttpStatusCode.OK, (await _http.PostAsync($"/api/v1/proposals/{id}/finalize", null)).StatusCode);

    Assert.Equal(HttpStatusCode.Conflict, (await _http.PostAsync($"/api/v1/proposals/{id}/finalize", null)).StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, (await _http.PatchAsJsonAsync($"/api/v1/proposals/{id}", new { city = "Rio" })).StatusCode);
  }

  [InfraFact]
  public async Task Health_ShouldBeUp()
  {
    Assert.Equal(HttpStatusCode.OK, (await _http.GetAsync("/health")).StatusCode);
  }

  private async Task<Guid> CreateDraft(object body)
  {
    var response = await _http.PostAsJsonAsync("/api/v1/proposals", body);
    response.EnsureSuccessStatusCode();
    return (await Json(response)).GetProperty("id").GetGuid();
  }

  private async Task<int> PendingEvents(Guid id)
  {
    var doc = await _factory.Mongo().GetCollection<BsonDocument>("proposals")
        .Find(Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(id, GuidRepresentation.Standard)))
        .FirstAsync();
    return doc["pendingEvents"].AsBsonArray.Count;
  }

  private static async Task<JsonElement> Json(HttpResponseMessage response)
      => await response.Content.ReadFromJsonAsync<JsonElement>();

  private static object CompleteData(string state = "SP") => new
  {
    fullName = "Integracao Onboarding",
    taxId = "52998224725",
    email = "it@email.com",
    phone = "11999998888",
    birthDate = "1990-05-15",
    street = "Rua A, 100",
    city = "Sao Paulo",
    state,
    postalCode = "01310100",
    motherName = "Maria Souza"
  };
}
