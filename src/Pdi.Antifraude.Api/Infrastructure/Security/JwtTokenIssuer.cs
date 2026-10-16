using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Pdi.Antifraude.Api.Infrastructure.Security;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAt, string Username, string DisplayName);

/// Confere usuário/senha e emite um JWT assinado (HS256).
/// O "sub" do token é quem aparece como DecidedBy na análise.
public sealed class JwtTokenIssuer
{
  public const string DisplayNameClaim = "name";

  private readonly AuthOptions _options;
  private readonly TimeProvider _time;

  public JwtTokenIssuer(IOptions<AuthOptions> options, TimeProvider time)
  {
    _options = options.Value;
    _time = time;
  }

  public static SymmetricSecurityKey SigningKey(AuthOptions options)
      => new(Encoding.UTF8.GetBytes(options.SigningKey));

  public LoginResponse? Login(LoginRequest request)
  {
    var user = _options.Users.FirstOrDefault(u =>
        string.Equals(u.Username, request.Username?.Trim(), StringComparison.OrdinalIgnoreCase));

    // Compara mesmo quando o usuário não existe: o tempo de resposta não revela
    // se o erro foi no usuário ou na senha.
    var expected = Encoding.UTF8.GetBytes(user?.Password ?? Guid.NewGuid().ToString());
    var provided = Encoding.UTF8.GetBytes(request.Password ?? string.Empty);
    if (!CryptographicOperations.FixedTimeEquals(expected, provided) || user is null)
      return null;

    var now = _time.GetUtcNow().UtcDateTime;
    var expiresAt = now.AddMinutes(_options.TokenLifetimeMinutes);

    var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
      Issuer = _options.Issuer,
      Audience = _options.Audience,
      IssuedAt = now,
      NotBefore = now,
      Expires = expiresAt,
      Claims = new Dictionary<string, object>
      {
        [JwtRegisteredClaimNames.Sub] = user.Username,
        [DisplayNameClaim] = user.DisplayName
      },
      SigningCredentials = new SigningCredentials(SigningKey(_options), SecurityAlgorithms.HmacSha256)
    });

    return new LoginResponse(token, expiresAt, user.Username, user.DisplayName);
  }
}
