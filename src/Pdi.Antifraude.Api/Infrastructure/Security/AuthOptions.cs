namespace Pdi.Antifraude.Api.Infrastructure.Security;

public sealed class AuthOptions
{
  public const string SectionName = "Auth";

  /// HS256 exige chave de pelo menos 256 bits.
  public const int MinSigningKeyBytes = 32;

  public string Issuer { get; set; } = "pdi-antifraude";
  public string Audience { get; set; } = "pdi-antifraude-ui";
  public string SigningKey { get; set; } = string.Empty;
  public int TokenLifetimeMinutes { get; set; } = 60;

  /// Usuários de DEMONSTRAÇÃO. Em produção isso viria de um provedor de identidade
  /// (Entra ID, Keycloak, Cognito...), e esta API só validaria o token.
  public List<AuthUser> Users { get; set; } = [];
}

public sealed class AuthUser
{
  public string Username { get; set; } = string.Empty;
  public string Password { get; set; } = string.Empty;
  public string DisplayName { get; set; } = string.Empty;
}
