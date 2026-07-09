using MovieBooking.Application.Common.DTOs;

namespace MovieBooking.Application.Common.Interfaces;

public interface ILoyaltyService
{
    Task<LoyaltyWalletDto> GetWalletAsync(Guid userId, CancellationToken cancellationToken = default);
    Task EarnForBookingAsync(Guid bookingId, decimal paidAmount, CancellationToken cancellationToken = default);
    Task RefundForBookingAsync(Guid bookingId, decimal paidAmount, CancellationToken cancellationToken = default);
    Task RedeemForBookingAsync(Guid bookingId, int usedPoints, CancellationToken cancellationToken = default);
    Task ReturnRedeemedPointsAsync(Guid bookingId, CancellationToken cancellationToken = default);
}
