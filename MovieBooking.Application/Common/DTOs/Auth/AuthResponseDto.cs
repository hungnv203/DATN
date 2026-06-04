using MovieBooking.Application.Common.DTOs;

namespace MovieBooking.Application.Common.DTOs.Auth;

public class AuthResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public UserDto User { get; set; } = null!;
}
