using MovieBooking.Application.Common.DTOs;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Application.Common.Interfaces;

public interface IPaymentService : ICrudService<Payment, PaymentDto>
{
    Task<(bool Success, string Url, string Message)> CreatePaymentUrlAsync(
        Guid bookingId, 
        string ipAddress, 
        Guid? currentUserId, 
        bool isAdminOrManager, 
        CancellationToken cancellationToken = default);

    Task<(PaymentDto? Dto, bool Found, bool Authorized)> GetPaymentDetailForUserAsync(
        Guid id,
        Guid? currentUserId,
        bool isAdminOrManager,
        CancellationToken cancellationToken = default);

    Task<(bool Found, decimal Amount)> GetPaymentVerificationInfoAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default);

    Task<Guid?> GetBookingIdByPaymentIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default);
}


