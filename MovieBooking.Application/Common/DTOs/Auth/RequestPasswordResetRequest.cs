namespace MovieBooking.Application.Common.DTOs.Auth;

public class RequestPasswordResetRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.EmailAddress]
    public string Email { get; set; } = string.Empty;
}
