using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pdi.Antifraude.Api.Infrastructure.Security;

namespace Pdi.Antifraude.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Tags("Auth")]
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
  private readonly JwtTokenIssuer _issuer;

  public AuthController(JwtTokenIssuer issuer)
  {
    _issuer = issuer;
  }

  [HttpPost("login")]
  [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public IActionResult Login([FromBody] LoginRequest request)
  {
    var response = _issuer.Login(request);
    return response is null ? Unauthorized() : Ok(response);
  }
}
