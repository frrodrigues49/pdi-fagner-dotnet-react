using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
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

    // Botão "Authorize" no Swagger: cole o accessToken do /api/v1/auth/login.
    var bearer = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    options.AddSecurityDefinition("Bearer", bearer);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearer] = [] });
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

var auth = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
if (System.Text.Encoding.UTF8.GetByteCount(auth.SigningKey) < AuthOptions.MinSigningKeyBytes)
    throw new InvalidOperationException($"Auth:SigningKey must have at least {AuthOptions.MinSigningKeyBytes} bytes.");

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<JwtTokenIssuer>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // mantém "sub" como "sub"
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = auth.Issuer,
            ValidAudience = auth.Audience,
            IssuerSigningKey = JwtTokenIssuer.SigningKey(auth),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
