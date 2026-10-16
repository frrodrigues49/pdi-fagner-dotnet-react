using System.Net;

namespace Pdi.Onboarding.UnitTests.Fakes;

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
  private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

  public StubHttpMessageHandler(HttpStatusCode statusCode)
      : this(_ => new HttpResponseMessage(statusCode)) { }

  public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
  {
    _respond = respond;
  }

  public HttpRequestMessage? LastRequest { get; private set; }
  public string? LastBody { get; private set; }

  protected override async Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
  {
    LastRequest = request;
    LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
    return _respond(request);
  }
}