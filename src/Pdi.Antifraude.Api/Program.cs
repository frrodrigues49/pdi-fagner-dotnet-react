using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using Pdi.Antifraude.Api.Application.Analyses;
using Pdi.Antifraude.Api.Application.Events;
using Pdi.Antifraude.Api.Infrastructure.Messaging;
using Pdi.Antifraude.Api.Infrastructure.Mongo;
using Pdi.Antifraude.Api.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PDI Antifraude API",
        Version = "v1",
        Description = "Fraud analysis of finalized proposals: approve or reject."
    });
});
builder.Services.AddHealthChecks();

builder.Services.Configure<KafkaOptions>(
    builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();

MongoConventions.Register();
builder.Services.Configure<MongoOptions>(
    builder.Configuration.GetSection(MongoOptions.SectionName));
builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(sp.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString));
builder.Services.AddSingleton<IMongoDatabase>(sp =>
    sp.GetRequiredService<IMongoClient>()
      .GetDatabase(sp.GetRequiredService<IOptions<MongoOptions>>().Value.Database));

builder.Services.AddSingleton<IOutboxStore, MongoAnalysisOutboxStore>();
builder.Services.AddHostedService<OutboxDispatcher>();

builder.Services.Configure<InternalApiOptions>(
    builder.Configuration.GetSection(InternalApiOptions.SectionName));
builder.Services.AddScoped<InternalApiKeyFilter>();

builder.Services.AddSingleton<CreateAnalysisValidator>();
builder.Services.AddSingleton<RejectAnalysisValidator>();
builder.Services.AddScoped<IAnalysisRepository, MongoAnalysisRepository>();
builder.Services.AddScoped<IAnalysisService, AnalysisService>();

var app = builder.Build();

await MongoIndexes.EnsureAsync(app.Services.GetRequiredService<IMongoDatabase>());

app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
