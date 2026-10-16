using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;
using Pdi.IntegrationTests.Support;

namespace Pdi.IntegrationTests.Apis;

// TEntryPoint = um tipo do assembly da API. Não dá para usar `Program`: as duas APIs e o
// Worker têm um `Program` top-level no namespace global, e eles colidiriam neste projeto.

public sealed class OnboardingApiFactory : WebApplicationFactory<Pdi.Onboarding.Api.Controllers.ProposalsController>
{
  public string Database { get; } = Infra.Unique("it-precadastro");
  public RecordingOnboardingPublisher Publisher { get; } = new();

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseEnvironment("Development");
    builder.UseSetting("Mongo:ConnectionString", Infra.Mongo);
    builder.UseSetting("Mongo:Database", Database);
    builder.ConfigureTestServices(services =>
    {
      services.RemoveAll<Pdi.Onboarding.Api.Application.Events.IEventPublisher>();
      services.AddSingleton<Pdi.Onboarding.Api.Application.Events.IEventPublisher>(Publisher);
    });
  }

  public IMongoDatabase Mongo() => new MongoClient(Infra.Mongo).GetDatabase(Database);

  protected override void Dispose(bool disposing)
  {
    base.Dispose(disposing);
    if (disposing && Infra.IsAvailable)
      new MongoClient(Infra.Mongo).DropDatabase(Database);
  }
}

public sealed class AntifraudeApiFactory : WebApplicationFactory<Pdi.Antifraude.Api.Controllers.AnalysesController>
{
  public const string InternalKey = "chave-interna-de-teste";

  public string Database { get; } = Infra.Unique("it-antifraude");
  public RecordingAntifraudePublisher Publisher { get; } = new();

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseEnvironment("Development");
    builder.UseSetting("Mongo:ConnectionString", Infra.Mongo);
    builder.UseSetting("Mongo:Database", Database);
    builder.UseSetting("InternalApi:Key", InternalKey);
    builder.ConfigureTestServices(services =>
    {
      services.RemoveAll<Pdi.Antifraude.Api.Application.Events.IEventPublisher>();
      services.AddSingleton<Pdi.Antifraude.Api.Application.Events.IEventPublisher>(Publisher);
    });
  }

  public IMongoDatabase Mongo() => new MongoClient(Infra.Mongo).GetDatabase(Database);

  protected override void Dispose(bool disposing)
  {
    base.Dispose(disposing);
    if (disposing && Infra.IsAvailable)
      new MongoClient(Infra.Mongo).DropDatabase(Database);
  }
}
