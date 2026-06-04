using MovieBooking.Application.Common.DTOs.Auth;

namespace MovieBooking.Application.Common.Interfaces;

public interface IUserService
{
    Task<AuthResponseDto> SignUpAsync(SignUpRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto?> SignInAsync(SignInRequest request, CancellationToken cancellationToken = default);
    Task<PasswordResetRequestResponseDto> RequestPasswordResetAsync(RequestPasswordResetRequest request, CancellationToken cancellationToken = default);
    Task<bool> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
}
