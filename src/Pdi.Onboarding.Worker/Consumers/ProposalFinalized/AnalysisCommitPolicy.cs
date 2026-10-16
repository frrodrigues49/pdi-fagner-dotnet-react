using System.Net;
using Pdi.Onboarding.Worker.Kafka;

namespace Pdi.Onboarding.Worker.Consumers.ProposalFinalized;

public static class AnalysisCommitPolicy
{
  public static ConsumerOutcome Decide(HttpStatusCode statusCode)
  {
    var code = (int)statusCode;

    // 201 criada, 204 já existia (replay): sucesso
    if (code is >= 200 and < 300)
      return ConsumerOutcome.Commit;

    // 400: o problema é a MENSAGEM e ela nunca vai passar (poison). Vai para a DLQ.
    if (statusCode == HttpStatusCode.BadRequest)
      return ConsumerOutcome.DeadLetter;

    // 401 (chave interna errada), 404 (URL errada), 429, 5xx: o problema é o AMBIENTE
    // (config, deploy, API fora). A mensagem é boa; quando o ambiente for corrigido, ela passa.
    return ConsumerOutcome.Retry;
  }
}
