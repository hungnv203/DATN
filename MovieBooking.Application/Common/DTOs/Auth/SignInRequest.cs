namespace MovieBooking.Application.Common.DTOs.Auth;

public class SignInRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.EmailAddress]
    public string Email { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required]
    public string Password { get; set; } = string.Empty;
}
