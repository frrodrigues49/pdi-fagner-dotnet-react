using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pdi.Antifraude.Api.Application.Analyses;
using Pdi.Antifraude.Api.Domain.Enums;
using Pdi.Antifraude.Api.Infrastructure.Security;

namespace Pdi.Antifraude.Api.Controllers;

[ApiController]
[Route("api/v1/analyses")]
[Tags("Analyses")]
[Authorize] // tela de aprovação: exige JWT (POST /api/v1/auth/login)
public sealed class AnalysesController : ControllerBase
{
  private readonly IAnalysisService _service;

  public AnalysesController(IAnalysisService service)
  {
    _service = service;
  }

  /// Uso interno: chamado pelo consumer de proposta-conta-finalizada (exige X-Internal-Api-Key).
  [HttpPost]
  [AllowAnonymous] // não é usuário: é serviço → protegido pela API key, não pelo JWT
  [ServiceFilter(typeof(InternalApiKeyFilter))]
  [ProducesResponseType(typeof(AnalysisResponse), StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> Create(
      [FromBody] CreateAnalysisRequest request,
      CancellationToken cancellationToken)
  {
    var result = await _service.CreateAsync(request, cancellationToken);
    return result.Status switch
    {
      AnalysisOperationStatus.Created => Created($"/api/v1/analyses/{result.Data!.Id}", result.Data),
      AnalysisOperationStatus.AlreadyExists => NoContent(),
      _ => ToActionResult(result)
    };
  }

  [HttpGet]
  [ProducesResponseType(typeof(IReadOnlyList<AnalysisResponse>), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> List(
      [FromQuery] AnalysisStatus? status,
      CancellationToken cancellationToken)
      => Ok(await _service.ListAsync(status, cancellationToken));

  [HttpGet("{id:guid}")]
  [ProducesResponseType(typeof(AnalysisResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
      => ToActionResult(await _service.GetAsync(id, cancellationToken));

  [HttpPost("{id:guid}/approve")]
  [ProducesResponseType(typeof(AnalysisResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
      => ToActionResult(await _service.ApproveAsync(id, CurrentUser, cancellationToken));

  [HttpPost("{id:guid}/reject")]
  [ProducesResponseType(typeof(AnalysisResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<IActionResult> Reject(
      Guid id,
      [FromBody] RejectAnalysisRequest request,
      CancellationToken cancellationToken)
      => ToActionResult(await _service.RejectAsync(id, request, CurrentUser, cancellationToken));

  // NameClaimType = "sub" (configurado no Program): o usuário que fez login.
  private string CurrentUser => User.Identity?.Name ?? throw new InvalidOperationException("Authenticated user without name.");

  private IActionResult ToActionResult(AnalysisResult result) => result.Status switch
  {
    AnalysisOperationStatus.Success => Ok(result.Data),
    AnalysisOperationStatus.NotFound => NotFound(),
    AnalysisOperationStatus.ValidationFailed => BadRequest(new { errors = result.Errors }),
    _ => Conflict(new { errors = result.Errors })
  };
}
