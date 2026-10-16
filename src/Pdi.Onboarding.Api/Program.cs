using Microsoft.OpenApi.Models;
using Pdi.Onboarding.Api.Application.Events;
using Pdi.Onboarding.Api.Infrastructure.Messaging;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Pdi.Onboarding.Api.Application.Proposals;
using Pdi.Onboarding.Api.Infrastructure.Mongo;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PDI Onboarding API",
        Version = "v1",
        Description = "Customer onboarding: proposal (pre-registration) drafting and finalization."
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

builder.Services.AddSingleton<IOutboxStore, MongoProposalOutboxStore>();
builder.Services.AddHostedService<OutboxDispatcher>();

builder.Services.AddSingleton<ProposalValidator>();
builder.Services.AddScoped<IProposalRepository, MongoProposalRepository>();
builder.Services.AddScoped<IProposalService, ProposalService>();

var app = builder.Build();

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
