using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Pdi.Antifraude.Api.Infrastructure.Security;

namespace Pdi.Antifraude.UnitTests;

public sealed class JwtTokenIssuerTests
{
  private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

  private readonly AuthOptions _options = new()
  {
    Issuer = "pdi-antifraude",
    Audience = "pdi-antifraude-ui",
    SigningKey = "chave-de-teste-com-pelo-menos-32-bytes!!",
    TokenLifetimeMinutes = 60,
    Users = [new AuthUser { Username = "analista", Password = "analista123", DisplayName = "Ana Analista" }]
  };

  private JwtTokenIssuer Issuer() => new(Options.Create(_options), new FixedTimeProvider(Now));

  [Fact]
  public async Task Login_WhenCredentialsAreValid_ShouldIssueSignedTokenWithUserAsSubject()
  {
    var response = Issuer().Login(new LoginRequest(" ANALISTA ", "analista123"));

    Assert.NotNull(response);
    Assert.Equal("analista", response.Username);
    Assert.Equal("Ana Analista", response.DisplayName);
    Assert.Equal(Now.UtcDateTime.AddMinutes(60), response.ExpiresAt);

    // Valida com os MESMOS parâmetros que a API usa no AddJwtBearer.
    var validation = await new JsonWebTokenHandler().ValidateTokenAsync(response.AccessToken, new TokenValidationParameters
    {
      ValidIssuer = _options.Issuer,
      ValidAudience = _options.Audience,
      IssuerSigningKey = JwtTokenIssuer.SigningKey(_options),
      ValidateLifetime = false
    });

    Assert.True(validation.IsValid);
    Assert.Equal("analista", validation.Claims[JwtRegisteredClaimNames.Sub]);
    Assert.Equal("Ana Analista", validation.Claims[JwtTokenIssuer.DisplayNameClaim]);
  }

  [Fact]
  public async Task Login_TokenSignedWithAnotherKey_ShouldBeRejected()
  {
    var response = Issuer().Login(new LoginRequest("analista", "analista123"))!;

    var validation = await new JsonWebTokenHandler().ValidateTokenAsync(response.AccessToken, new TokenValidationParameters
    {
      ValidIssuer = _options.Issuer,
      ValidAudience = _options.Audience,
      IssuerSigningKey = JwtTokenIssuer.SigningKey(new AuthOptions { SigningKey = "outra-chave-qualquer-com-32-bytes-ou-mais" }),
      ValidateLifetime = false
    });

    Assert.False(validation.IsValid);
  }

  [Theory]
  [InlineData("analista", "senha-errada")]
  [InlineData("analista", "")]
  [InlineData("ninguem", "analista123")]
  [InlineData("", "")]
  public void Login_WhenCredentialsAreInvalid_ShouldReturnNull(string username, string password)
  {
    Assert.Null(Issuer().Login(new LoginRequest(username, password)));
  }

  private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
  {
    public override DateTimeOffset GetUtcNow() => now;
  }
}
