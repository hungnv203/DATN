using AutoMapper;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

internal sealed class PaymentService : IPaymentService
{
    private readonly EntityCrudOperations<Payment, PaymentDto> _operations;

    public PaymentService(AppDbContext dbContext, IMapper mapper)
    {
        _operations = new EntityCrudOperations<Payment, PaymentDto>(dbContext, mapper);
    }

    public Task<IReadOnlyList<PaymentDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _operations.GetAllAsync(cancellationToken);

    public Task<PaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.GetByIdAsync(id, cancellationToken);

    public Task<PaymentDto> CreateAsync(PaymentDto dto, CancellationToken cancellationToken = default) =>
        _operations.CreateAsync(dto, cancellationToken);

    public Task<bool> UpdateAsync(Guid id, PaymentDto dto, CancellationToken cancellationToken = default) =>
        _operations.UpdateAsync(id, dto, cancellationToken);

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.DeleteAsync(id, cancellationToken);
}

