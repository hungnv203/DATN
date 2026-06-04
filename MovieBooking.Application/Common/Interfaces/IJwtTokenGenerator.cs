namespace MovieBooking.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    (string Token, DateTimeOffset ExpiresAtUtc) CreateAccessToken(Guid userId, string email, string fullName);
}
