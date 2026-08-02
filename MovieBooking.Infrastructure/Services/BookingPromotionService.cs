using AutoMapper;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

internal sealed class BookingPromotionService : IBookingPromotionService
{
    private readonly EntityCrudOperations<BookingPromotion, BookingPromotionDto> _operations;

    public BookingPromotionService(AppDbContext dbContext, IMapper mapper)
    {
        _operations = new EntityCrudOperations<BookingPromotion, BookingPromotionDto>(dbContext, mapper);
    }

    public Task<IReadOnlyList<BookingPromotionDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _operations.GetAllAsync(cancellationToken);

    public Task<BookingPromotionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.GetByIdAsync(id, cancellationToken);

    public Task<BookingPromotionDto> CreateAsync(BookingPromotionDto dto, CancellationToken cancellationToken = default) =>
        _operations.CreateAsync(dto, cancellationToken);

    public Task<bool> UpdateAsync(Guid id, BookingPromotionDto dto, CancellationToken cancellationToken = default) =>
        _operations.UpdateAsync(id, dto, cancellationToken);

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.DeleteAsync(id, cancellationToken);
}

