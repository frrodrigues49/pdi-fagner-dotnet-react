using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Pdi.Antifraude.Api.Infrastructure.Security;

/// Protege rotas de uso interno (serviço → serviço) com uma chave no header.
/// Uso: [ServiceFilter(typeof(InternalApiKeyFilter))]
public sealed class InternalApiKeyFilter : IActionFilter
{
  public const string HeaderName = "X-Internal-Api-Key";

  private readonly byte[] _expected;

  public InternalApiKeyFilter(IOptions<InternalApiOptions> options)
  {
    _expected = Encoding.UTF8.GetBytes(options.Value.Key);
  }

  public void OnActionExecuting(ActionExecutingContext context)
  {
    var provided = Encoding.UTF8.GetBytes(
        context.HttpContext.Request.Headers[HeaderName].ToString());

    // Fail closed: sem chave configurada, ninguém entra.
    // FixedTimeEquals evita descobrir a chave medindo o tempo da comparação.
    if (_expected.Length == 0 || !CryptographicOperations.FixedTimeEquals(provided, _expected))
      context.Result = new UnauthorizedResult();
  }

  public void OnActionExecuted(ActionExecutedContext context)
  {
  }
}
