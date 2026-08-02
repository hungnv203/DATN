using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

public class UserManagementService : IUserManagementService
{
    private readonly EntityCrudOperations<User, UserDto> _operations;
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IMapper _mapper;

    public UserManagementService(AppDbContext db, IMapper mapper, IPasswordHasher passwordHasher)
    {
        _operations = new EntityCrudOperations<User, UserDto>(db, mapper);
        _db = db;
        _mapper = mapper;
        _passwordHasher = passwordHasher;
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Users
            .AsNoTracking()
            .OrderBy(user => user.Email)
            .Take(200)
            .Select(user => new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Status = user.Status,
                RoleName = user.UserRoles
                    .Select(userRole => userRole.Role.Name)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FindAsync([id], cancellationToken);
        if (user == null) return null;

        var dto = _mapper.Map<UserDto>(user);
        dto.RoleName = await _db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.Role.Name)
            .FirstOrDefaultAsync(cancellationToken);
        return dto;
    }

    public async Task<UserDto> CreateAsync(UserDto dto, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        var emailExists = await _db.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            throw new InvalidOperationException("Email is already registered.");
        }

        var user = new User
        {
            FullName = dto.FullName,
            Email = normalizedEmail,
            PhoneNumber = dto.PhoneNumber,
            Status = string.IsNullOrWhiteSpace(dto.Status) ? "Active" : dto.Status,
            PasswordHash = _passwordHasher.Hash(string.IsNullOrWhiteSpace(dto.Password) ? "123456" : dto.Password)
        };

        await _db.Users.AddAsync(user, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        // Assign Role
        var roleName = string.IsNullOrWhiteSpace(dto.RoleName) ? "Customer" : dto.RoleName.Trim();
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
        if (role == null)
        {
            role = new Role { Name = roleName, Description = $"{roleName} role" };
            await _db.Roles.AddAsync(role, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var userRole = new UserRole { UserId = user.Id, RoleId = role.Id };
        await _db.UserRoles.AddAsync(userRole, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var resultDto = _mapper.Map<UserDto>(user);
        resultDto.RoleName = roleName;
        return resultDto;
    }

    public async Task<bool> UpdateAsync(Guid id, UserDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FindAsync([id], cancellationToken);
        if (user == null) return false;

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        var emailExists = await _db.Users.AnyAsync(
            u => u.Id != id && u.Email == normalizedEmail,
            cancellationToken);
        if (emailExists)
        {
            throw new InvalidOperationException("Email is already registered.");
        }

        user.FullName = dto.FullName;
        user.Email = normalizedEmail;
        user.PhoneNumber = dto.PhoneNumber;
        user.Status = dto.Status;

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            user.PasswordHash = _passwordHasher.Hash(dto.Password);
        }

        user.MarkUpdated(DateTimeOffset.UtcNow);

        // Manage Role
        if (dto.RoleName != null) // If role name is passed (even if empty to remove)
        {
            // Remove existing user roles
            var existingUserRoles = await _db.UserRoles.Where(ur => ur.UserId == user.Id).ToListAsync(cancellationToken);
            _db.UserRoles.RemoveRange(existingUserRoles);
            await _db.SaveChangesAsync(cancellationToken);

            var roleName = dto.RoleName.Trim();
            if (roleName.Length > 0)
            {
                var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
                if (role == null)
                {
                    role = new Role { Name = roleName };
                    await _db.Roles.AddAsync(role, cancellationToken);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                var userRole = new UserRole { UserId = user.Id, RoleId = role.Id };
                await _db.UserRoles.AddAsync(userRole, cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.DeleteAsync(id, cancellationToken);
}

