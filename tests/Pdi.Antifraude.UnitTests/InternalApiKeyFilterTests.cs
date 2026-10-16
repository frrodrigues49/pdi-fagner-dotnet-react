using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Pdi.Antifraude.Api.Infrastructure.Security;

namespace Pdi.Antifraude.UnitTests;

public sealed class InternalApiKeyFilterTests
{
  private const string Key = "segredo-de-teste";

  [Fact]
  public void OnActionExecuting_WhenKeyMatches_ShouldLetRequestThrough()
  {
    var context = Execute(configuredKey: Key, headerValue: Key);

    Assert.Null(context.Result);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("chave-errada")]
  public void OnActionExecuting_WhenKeyIsMissingOrWrong_ShouldReturn401(string? headerValue)
  {
    var context = Execute(configuredKey: Key, headerValue: headerValue);

    Assert.IsType<UnauthorizedResult>(context.Result);
  }

  [Fact]
  public void OnActionExecuting_WhenNoKeyIsConfigured_ShouldFailClosed()
  {
    var context = Execute(configuredKey: "", headerValue: "");

    Assert.IsType<UnauthorizedResult>(context.Result);
  }

  private static ActionExecutingContext Execute(string configuredKey, string? headerValue)
  {
    var http = new DefaultHttpContext();
    if (headerValue is not null)
      http.Request.Headers[InternalApiKeyFilter.HeaderName] = headerValue;

    var context = new ActionExecutingContext(
        new ActionContext(http, new RouteData(), new ActionDescriptor()),
        new List<IFilterMetadata>(),
        new Dictionary<string, object?>(),
        controller: new object());

    new InternalApiKeyFilter(Options.Create(new InternalApiOptions { Key = configuredKey }))
        .OnActionExecuting(context);

    return context;
  }
}
