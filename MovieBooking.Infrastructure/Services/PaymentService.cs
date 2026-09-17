using AutoMapper;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;
using PaymentEntity = MovieBooking.Domain.Entities.Payment;

using Microsoft.EntityFrameworkCore;
using MovieBooking.Domain.Constants;

namespace MovieBooking.Infrastructure.Services;

internal sealed class PaymentService : IPaymentService
{
    private readonly EntityCrudOperations<PaymentEntity, PaymentDto> _operations;
    private readonly AppDbContext _db;
    private readonly IVnPayService _vnPayService;

    public PaymentService(AppDbContext dbContext, IMapper mapper, IVnPayService vnPayService)
    {
        _operations = new EntityCrudOperations<PaymentEntity, PaymentDto>(dbContext, mapper);
        _db = dbContext;
        _vnPayService = vnPayService;
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

    public async Task<(bool Success, string Url, string Message)> CreatePaymentUrlAsync(
        Guid bookingId, 
        string ipAddress, 
        Guid? currentUserId, 
        bool isAdminOrManager, 
        CancellationToken cancellationToken = default)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(
            item => item.Id == bookingId,
            cancellationToken);
        
        if (booking == null)
        {
            return (false, string.Empty, "Booking was not found.");
        }

        if (!isAdminOrManager && booking.UserId != currentUserId)
        {
            return (false, string.Empty, "Forbidden");
        }

        if (booking.Channel != BookingChannels.CustomerOnline
            || booking.Status != BookingStatuses.Pending)
        {
            return (false, string.Empty, "Booking is not eligible for online payment.");
        }

        var payment = await _db.Payments.SingleOrDefaultAsync(
            item => item.BookingId == booking.Id,
            cancellationToken);
            
        if (payment != null && payment.Status != PaymentStatuses.Pending)
        {
            return (false, string.Empty, "Payment is already finalized.");
        }

        if (payment == null)
        {
            payment = new PaymentEntity
            {
                BookingId = booking.Id,
                Amount = booking.TotalPrice,
                Method = PaymentMethods.VnPay,
                Status = PaymentStatuses.Pending,
                TransactionCode = string.Empty
            };
            _db.Payments.Add(payment);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return (true, _vnPayService.CreatePaymentUrl(ipAddress, payment, booking), string.Empty);
    }

    public async Task<(PaymentDto? Dto, bool Found, bool Authorized)> GetPaymentDetailForUserAsync(
        Guid id,
        Guid? currentUserId,
        bool isAdminOrManager,
        CancellationToken cancellationToken = default)
    {
        var payment = await _db.Payments
            .AsNoTracking()
            .Include(item => item.Booking)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (payment == null)
        {
            return (null, false, false);
        }

        var isOwner = currentUserId.HasValue && payment.Booking != null && payment.Booking.UserId == currentUserId.Value;
        if (!isAdminOrManager && !isOwner)
        {
            return (null, true, false);
        }

        var dto = new PaymentDto
        {
            Id = payment.Id,
            BookingId = payment.BookingId,
            Amount = payment.Amount,
            Method = payment.Method,
            Status = payment.Status,
            TransactionCode = string.Empty
        };

        return (dto, true, true);
    }

    public async Task<(bool Found, decimal Amount)> GetPaymentVerificationInfoAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _db.Payments
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == paymentId, cancellationToken);

        if (payment == null)
        {
            return (false, 0m);
        }

        return (true, payment.Amount);
    }

    public async Task<Guid?> GetBookingIdByPaymentIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Payments
            .AsNoTracking()
            .Where(item => item.Id == paymentId)
            .Select(item => (Guid?)item.BookingId)
            .SingleOrDefaultAsync(cancellationToken);
    }
}

