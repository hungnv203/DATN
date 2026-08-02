using AutoMapper;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

internal sealed class SeatHoldService : ISeatHoldService
{
    private readonly EntityCrudOperations<SeatHold, SeatHoldDto> _operations;

    public SeatHoldService(AppDbContext dbContext, IMapper mapper)
    {
        _operations = new EntityCrudOperations<SeatHold, SeatHoldDto>(dbContext, mapper);
    }

    public Task<IReadOnlyList<SeatHoldDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _operations.GetAllAsync(cancellationToken);

    public Task<SeatHoldDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.GetByIdAsync(id, cancellationToken);

    public Task<SeatHoldDto> CreateAsync(SeatHoldDto dto, CancellationToken cancellationToken = default) =>
        _operations.CreateAsync(dto, cancellationToken);

    public Task<bool> UpdateAsync(Guid id, SeatHoldDto dto, CancellationToken cancellationToken = default) =>
        _operations.UpdateAsync(id, dto, cancellationToken);

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.DeleteAsync(id, cancellationToken);
}

