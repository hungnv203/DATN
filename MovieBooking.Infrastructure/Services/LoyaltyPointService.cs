using AutoMapper;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

internal sealed class LoyaltyPointService : ILoyaltyPointService
{
    private readonly EntityCrudOperations<LoyaltyPoint, LoyaltyPointDto> _operations;

    public LoyaltyPointService(AppDbContext dbContext, IMapper mapper)
    {
        _operations = new EntityCrudOperations<LoyaltyPoint, LoyaltyPointDto>(dbContext, mapper);
    }

    public Task<IReadOnlyList<LoyaltyPointDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _operations.GetAllAsync(cancellationToken);

    public Task<LoyaltyPointDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.GetByIdAsync(id, cancellationToken);

    public Task<LoyaltyPointDto> CreateAsync(LoyaltyPointDto dto, CancellationToken cancellationToken = default) =>
        _operations.CreateAsync(dto, cancellationToken);

    public Task<bool> UpdateAsync(Guid id, LoyaltyPointDto dto, CancellationToken cancellationToken = default) =>
        _operations.UpdateAsync(id, dto, cancellationToken);

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.DeleteAsync(id, cancellationToken);
}

