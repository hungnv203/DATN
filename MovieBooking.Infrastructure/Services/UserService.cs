using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.DTOs.Auth;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

public class UserService : IUserService
{
    private static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromHours(1);

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IMapper _mapper;
    private readonly IHostEnvironment _environment;

    public UserService(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IMapper mapper,
        IHostEnvironment environment)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _mapper = mapper;
        _environment = environment;
    }

    public async Task<AuthResponseDto> SignUpAsync(SignUpRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("Email is already registered.");
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            PhoneNumber = request.PhoneNumber.Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            Status = "Active"
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        // Assign default Customer role
        var customerRoleName = "Customer";
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == customerRoleName, cancellationToken);
        if (role == null)
        {
            role = new Role { Name = customerRoleName, Description = "Customer role" };
            await _db.Roles.AddAsync(role, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var userRole = new UserRole { UserId = user.Id, RoleId = role.Id };
        await _db.UserRoles.AddAsync(userRole, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return BuildAuthResponse(user);
    }

    public async Task<AuthResponseDto?> SignInAsync(SignInRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.Email.ToLower() == normalizedEmail,
            cancellationToken);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return null;
        }

        return BuildAuthResponse(user);
    }

    public async Task<PasswordResetRequestResponseDto> RequestPasswordResetAsync(
        RequestPasswordResetRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.Email.ToLower() == normalizedEmail,
            cancellationToken);

        if (user is null)
        {
            return new PasswordResetRequestResponseDto
            {
                Message = "If an account exists for this email, password reset instructions have been sent."
            };
        }

        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var now = DateTimeOffset.UtcNow;
        user.SetPasswordReset(token, now.Add(PasswordResetTokenLifetime), now);
        await _db.SaveChangesAsync(cancellationToken);

        var response = new PasswordResetRequestResponseDto
        {
            Message = "If an account exists for this email, password reset instructions have been sent."
        };

        if (_environment.IsDevelopment())
        {
            response.ResetToken = token;
        }

        return response;
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.Email.ToLower() == normalizedEmail,
            cancellationToken);

        if (user is null
            || user.PasswordResetToken is null
            || user.PasswordResetExpires is null
            || user.PasswordResetExpires < DateTimeOffset.UtcNow
            || user.PasswordResetToken != request.Token.Trim())
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        user.SetPassword(_passwordHasher.Hash(request.NewPassword), now);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private AuthResponseDto BuildAuthResponse(User user)
    {
        var roles = _db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.Role.Name)
            .ToList();

        var (token, expiresAt) = _jwtTokenGenerator.CreateAccessToken(user.Id, user.Email, user.FullName, roles);
        return new AuthResponseDto
        {
            AccessToken = token,
            ExpiresAtUtc = expiresAt,
            User = _mapper.Map<UserDto>(user)
        };
    }
}
