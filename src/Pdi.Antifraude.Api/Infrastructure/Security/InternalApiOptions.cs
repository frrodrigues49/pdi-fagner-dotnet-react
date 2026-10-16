namespace Pdi.Antifraude.Api.Infrastructure.Security;

public sealed class InternalApiOptions
{
  public const string SectionName = "InternalApi";

  /// Segredo compartilhado com o Worker. Vazio = rota interna fechada para todos.
  public string Key { get; set; } = string.Empty;
}
