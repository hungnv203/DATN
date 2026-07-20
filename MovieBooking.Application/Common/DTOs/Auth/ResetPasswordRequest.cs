namespace MovieBooking.Application.Common.DTOs.Auth;

public class ResetPasswordRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.EmailAddress]
    public string Email { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required]
    public string Token { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(128, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;
}
