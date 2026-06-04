using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs.Auth;
using MovieBooking.Application.Common.Interfaces;

namespace MovieBooking.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;

    public AuthController(IUserService userService)
    {
        _userService = userService;
    }

    [AllowAnonymous]
    [HttpPost("sign-up")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponseDto>> SignUp([FromBody] SignUpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _userService.SignUpAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { message = "Email is already registered." });
        }
    }

    [AllowAnonymous]
    [HttpPost("sign-in")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> SignIn([FromBody] SignInRequest request, CancellationToken cancellationToken)
    {
        var result = await _userService.SignInAsync(request, cancellationToken);
        if (result is null)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("request-password-reset")]
    [ProducesResponseType(typeof(PasswordResetRequestResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PasswordResetRequestResponseDto>> RequestPasswordReset(
        [FromBody] RequestPasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _userService.RequestPasswordResetAsync(request, cancellationToken);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var ok = await _userService.ResetPasswordAsync(request, cancellationToken);
        if (!ok)
        {
            return BadRequest(new { message = "Invalid or expired reset token." });
        }

        return NoContent();
    }
}
