using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using MovieBooking.Application.Common.Configuration;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;

namespace MovieBooking.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("Assistant")]
[Route("api/assistant")]
public sealed class AssistantController : ControllerBase
{
    private readonly IAssistantService _assistantService;
    private readonly AssistantOptions _options;

    public AssistantController(
        IAssistantService assistantService,
        IOptions<AssistantOptions> options)
    {
        _assistantService = assistantService;
        _options = options.Value;
    }

    [HttpGet("availability")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetAvailability()
    {
        return Ok(new { enabled = _options.Enabled });
    }

    [HttpPost("messages")]
    [ProducesResponseType<AssistantResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AssistantResponseDto>> SendMessage(
        [FromBody] SendAssistantMessageRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _assistantService.SendAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
