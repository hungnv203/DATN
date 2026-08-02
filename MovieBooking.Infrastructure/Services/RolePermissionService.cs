using AutoMapper;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

internal sealed class RolePermissionService : IRolePermissionService
{
    private readonly EntityCrudOperations<RolePermission, RolePermissionDto> _operations;

    public RolePermissionService(AppDbContext dbContext, IMapper mapper)
    {
        _operations = new EntityCrudOperations<RolePermission, RolePermissionDto>(dbContext, mapper);
    }

    public Task<IReadOnlyList<RolePermissionDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _operations.GetAllAsync(cancellationToken);

    public Task<RolePermissionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.GetByIdAsync(id, cancellationToken);

    public Task<RolePermissionDto> CreateAsync(RolePermissionDto dto, CancellationToken cancellationToken = default) =>
        _operations.CreateAsync(dto, cancellationToken);

    public Task<bool> UpdateAsync(Guid id, RolePermissionDto dto, CancellationToken cancellationToken = default) =>
        _operations.UpdateAsync(id, dto, cancellationToken);

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.DeleteAsync(id, cancellationToken);
}

