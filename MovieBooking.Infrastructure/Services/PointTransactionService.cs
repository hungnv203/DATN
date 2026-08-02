using AutoMapper;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

internal sealed class PointTransactionService : IPointTransactionService
{
    private readonly EntityCrudOperations<PointTransaction, PointTransactionDto> _operations;

    public PointTransactionService(AppDbContext dbContext, IMapper mapper)
    {
        _operations = new EntityCrudOperations<PointTransaction, PointTransactionDto>(dbContext, mapper);
    }

    public Task<IReadOnlyList<PointTransactionDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _operations.GetAllAsync(cancellationToken);

    public Task<PointTransactionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.GetByIdAsync(id, cancellationToken);

    public Task<PointTransactionDto> CreateAsync(PointTransactionDto dto, CancellationToken cancellationToken = default) =>
        _operations.CreateAsync(dto, cancellationToken);

    public Task<bool> UpdateAsync(Guid id, PointTransactionDto dto, CancellationToken cancellationToken = default) =>
        _operations.UpdateAsync(id, dto, cancellationToken);

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.DeleteAsync(id, cancellationToken);
}

