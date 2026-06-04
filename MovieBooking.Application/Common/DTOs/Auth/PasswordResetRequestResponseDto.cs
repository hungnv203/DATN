namespace MovieBooking.Application.Common.DTOs.Auth;

public class PasswordResetRequestResponseDto
{
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Populated only in Development when the account exists, so you can test reset without email.
    /// </summary>
    public string? ResetToken { get; set; }
}
