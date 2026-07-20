namespace MovieBooking.Application.Common.DTOs.Auth;

public class SignUpRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(120, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.EmailAddress]
    public string Email { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.Phone]
    public string PhoneNumber { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}
