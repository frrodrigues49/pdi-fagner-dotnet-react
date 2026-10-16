namespace Pdi.Onboarding.Worker.Kafka;

public enum ConsumerOutcome
{
  /// Terminou (sucesso ou replay): pode comitar o offset.
  Commit,

  /// Falha transitória: NÃO comita, espera e processa a mesma mensagem de novo.
  Retry,

  /// Nunca vai passar (poison): manda para <tópico>.dlq e comita, para não travar a partição.
  DeadLetter
}
