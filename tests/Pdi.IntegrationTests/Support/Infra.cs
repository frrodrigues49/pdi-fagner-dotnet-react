using System.Net.Sockets;

namespace Pdi.IntegrationTests.Support;

/// Endereços da infraestrutura real (a mesma do docker-compose).
/// Localmente: `docker compose up -d`. No CI: o workflow sobe antes dos testes.
public static class Infra
{
  public static string Kafka => Env("PDI_KAFKA", "localhost:9092");
  public static string Mongo => Env("PDI_MONGO", "mongodb://localhost:27017");
  public static string Oracle => Env("PDI_ORACLE", "User Id=pdi;Password=pdi;Data Source=localhost:1521/FREEPDB1");

  /// No CI (PDI_REQUIRE_INFRA=1) infra fora do ar é ERRO, não "pular teste".
  public static bool Required => Environment.GetEnvironmentVariable("PDI_REQUIRE_INFRA") == "1";

  private static readonly Lazy<bool> Available = new(() =>
      CanConnect("localhost", 9092) && CanConnect("localhost", 27017) && CanConnect("localhost", 1521));

  public static bool IsAvailable => Available.Value;

  /// Nome único por execução: os testes não se misturam com os dados da demo nem entre si.
  public static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 13, 60)];

  private static string Env(string name, string fallback) => Environment.GetEnvironmentVariable(name) ?? fallback;

  private static bool CanConnect(string host, int port)
  {
    try
    {
      using var client = new TcpClient();
      return client.ConnectAsync(host, port).Wait(TimeSpan.FromSeconds(1)) && client.Connected;
    }
    catch
    {
      return false;
    }
  }
}

/// [Fact] que vira "skipped" quando a infra não está no ar, em vez de falhar.
public sealed class InfraFactAttribute : FactAttribute
{
  public InfraFactAttribute()
  {
    if (!Infra.IsAvailable && !Infra.Required)
      Skip = "Infra fora do ar (Kafka/Mongo/Oracle). Rode `docker compose up -d`.";
  }
}
