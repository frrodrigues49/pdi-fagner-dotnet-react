using Microsoft.AspNetCore.Mvc;
using Pdi.Onboarding.Api.Application.Proposals;

namespace Pdi.Onboarding.Api.Controllers;

[ApiController]
[Route("api/v1/proposals")]
[Tags("Proposals")]
public sealed class ProposalsController : ControllerBase
{
  private readonly IProposalService _service;

  public ProposalsController(IProposalService service)
  {
    _service = service;
  }

  [HttpPost]
  [ProducesResponseType(typeof(ProposalResponse), StatusCodes.Status201Created)]
  public async Task<IActionResult> Create(
      [FromBody] ProposalDataRequest request,
      CancellationToken cancellationToken)
  {
    var result = await _service.CreateAsync(request, cancellationToken);
    return Created($"/api/v1/proposals/{result.Data!.Id}", result.Data);
  }

  [HttpGet("{id:guid}")]
  [ProducesResponseType(typeof(ProposalResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
      => ToActionResult(await _service.GetAsync(id, cancellationToken));

  [HttpPatch("{id:guid}")]
  [ProducesResponseType(typeof(ProposalResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<IActionResult> Update(
      Guid id,
      [FromBody] ProposalDataRequest request,
      CancellationToken cancellationToken)
      => ToActionResult(await _service.UpdateAsync(id, request, cancellationToken));

  [HttpPost("{id:guid}/finalize")]
  [ProducesResponseType(typeof(ProposalResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<IActionResult> Finalize(Guid id, CancellationToken cancellationToken)
      => ToActionResult(await _service.FinalizeAsync(id, cancellationToken));

  private IActionResult ToActionResult(ProposalResult result) => result.Status switch
  {
    ProposalOperationStatus.Success => Ok(result.Data),
    ProposalOperationStatus.NotFound => NotFound(),
    ProposalOperationStatus.ValidationFailed => BadRequest(new { errors = result.Errors }),
    _ => Conflict(new { errors = result.Errors })
  };
}