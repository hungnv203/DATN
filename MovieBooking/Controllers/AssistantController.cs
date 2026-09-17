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
    private readonly IAssistantService _service;
    private readonly AssistantOptions _options;

    public AssistantController(IAssistantService service, IOptions<AssistantOptions> options)
    {
        _service = service;
        _options = options.Value;
    }

    [HttpGet("availability")]
    public IActionResult Availability() => Ok(new { enabled = _options.Enabled });

    [HttpPost("messages")]
    public async Task<ActionResult<AssistantResponseDto>> Send(SendAssistantMessageRequestDto request, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.SendAsync(request, cancellationToken)); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }
}
