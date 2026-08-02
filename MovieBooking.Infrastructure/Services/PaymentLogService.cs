using AutoMapper;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

internal sealed class PaymentLogService : IPaymentLogService
{
    private readonly EntityCrudOperations<PaymentLog, PaymentLogDto> _operations;

    public PaymentLogService(AppDbContext dbContext, IMapper mapper)
    {
        _operations = new EntityCrudOperations<PaymentLog, PaymentLogDto>(dbContext, mapper);
    }

    public Task<IReadOnlyList<PaymentLogDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _operations.GetAllAsync(cancellationToken);

    public Task<PaymentLogDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.GetByIdAsync(id, cancellationToken);

    public Task<PaymentLogDto> CreateAsync(PaymentLogDto dto, CancellationToken cancellationToken = default) =>
        _operations.CreateAsync(dto, cancellationToken);

    public Task<bool> UpdateAsync(Guid id, PaymentLogDto dto, CancellationToken cancellationToken = default) =>
        _operations.UpdateAsync(id, dto, cancellationToken);

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.DeleteAsync(id, cancellationToken);
}

