using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Pdi.Onboarding.Contracts;
using Pdi.Onboarding.Worker.Antifraude;
using Pdi.Onboarding.Worker.Consumers.ProposalApproved;
using Pdi.Onboarding.Worker.Consumers.ProposalFinalized;
using Pdi.Onboarding.Worker.Consumers.ProposalRejected;
using Pdi.Onboarding.Worker.Kafka;
using Pdi.Onboarding.Worker.Mongo;
using Pdi.Onboarding.Worker.Proposals;
using Microsoft.EntityFrameworkCore;
using Pdi.Onboarding.Worker.Accounts;
using Pdi.Onboarding.Worker.Consumers.CreateAccount;
using Pdi.Onboarding.Worker.Oracle;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<KafkaOptions>(
    builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.Configure<AntifraudeOptions>(
    builder.Configuration.GetSection(AntifraudeOptions.SectionName));
builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();

MongoConventions.Register();
builder.Services.Configure<MongoOptions>(
    builder.Configuration.GetSection(MongoOptions.SectionName));
builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(sp.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString));
builder.Services.AddSingleton<IMongoDatabase>(sp =>
    sp.GetRequiredService<IMongoClient>()
      .GetDatabase(sp.GetRequiredService<IOptions<MongoOptions>>().Value.Database));
builder.Services.AddSingleton<IProposalStore, MongoProposalStore>();
builder.Services.AddDbContext<AccountsDbContext>(options =>
    options.UseOracle(builder.Configuration.GetConnectionString("Accounts")));
builder.Services.AddScoped<IAccountRepository, EfAccountRepository>();

builder.Services.AddHttpClient<AntifraudeApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<AntifraudeOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    client.DefaultRequestHeaders.Add(AntifraudeOptions.InternalApiKeyHeader, options.InternalApiKey);
});

builder.Services.AddKafkaConsumer<ProposalFinalizedEvent, ProposalFinalizedHandler>(
    Topics.ProposalFinalized,
    groupId: "consumer-proposta-finalizada");

builder.Services.AddKafkaConsumer<ProposalApprovedEvent, ProposalApprovedHandler>(
    Topics.ProposalApproved,
    groupId: "consumer-proposta-aprovada");

builder.Services.AddKafkaConsumer<ProposalRejectedEvent, ProposalRejectedHandler>(
    Topics.ProposalRejected,
    groupId: "consumer-proposta-rejeitada");

builder.Services.AddKafkaConsumer<CreateAccountEvent, CreateAccountHandler>(
    Topics.CreateAccount,
    groupId: "consumer-criar-conta");

var host = builder.Build();

// Cria/atualiza a tabela ACCOUNTS antes de consumir. Bom para a demo; em produção,
// migration roda no pipeline de deploy, não no startup de cada réplica.
using (var scope = host.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<AccountsDbContext>().Database.MigrateAsync();

host.Run();