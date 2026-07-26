using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

public class PricingService : IPricingService
{
    private const decimal VipSeatMarkup = 20000m;
    private const decimal CoupleSeatMarkup = 40000m;
    private const decimal PointValue = 1m;

    private readonly AppDbContext _db;

    public PricingService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<BookingQuoteDto> QuoteAsync(
        BookingQuoteRequestDto request,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var showtime = await _db.Showtimes
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.ShowtimeId, cancellationToken);
        if (showtime == null)
        {
            throw new InvalidOperationException("Không tìm thấy suất chiếu.");
        }

        if (request.SeatIds.Count == 0)
        {
            throw new InvalidOperationException("Danh sách ghế không được để trống.");
        }

        var seats = await _db.Seats
            .AsNoTracking()
            .Where(s => request.SeatIds.Contains(s.Id) && s.RoomId == showtime.RoomId)
            .ToListAsync(cancellationToken);
        if (seats.Count != request.SeatIds.Count)
        {
            throw new InvalidOperationException("Một hoặc nhiều ghế được chọn không hợp lệ.");
        }

        var reservedSeatExists = await _db.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.Booking)
            .AnyAsync(
                ticket => request.SeatIds.Contains(ticket.SeatId)
                          && ticket.Booking.ShowtimeId == request.ShowtimeId
                          && ticket.Booking.Status != "Cancelled"
                          && ticket.Booking.Status != "Expired",
                cancellationToken);
        if (reservedSeatExists)
        {
            throw new InvalidOperationException("One or more selected seats have already been reserved.");
        }

        var now = DateTime.UtcNow;
        var heldByAnotherUserExists = await _db.SeatHolds
            .AsNoTracking()
            .AnyAsync(
                hold => request.SeatIds.Contains(hold.SeatId)
                        && hold.ShowtimeId == request.ShowtimeId
                        && hold.Status == SeatHoldStatuses.Active
                        && hold.ExpiredAt > now
                        && (!userId.HasValue || hold.UserId != userId.Value),
                cancellationToken);
        if (heldByAnotherUserExists)
        {
            throw new InvalidOperationException("One or more selected seats are currently held by another user.");
        }

        var seatTotal = seats.Sum(seat =>
        {
            var price = showtime.BasePrice;
            if (seat.Type == "VIP")
            {
                price += VipSeatMarkup;
            }
            else if (seat.Type == "Couple")
            {
                price += CoupleSeatMarkup;
            }

            return price;
        });

        decimal concessionTotal = 0;
        if (request.Concessions.Count > 0)
        {
            var requestedItems = request.Concessions
                .Where(c => c.Quantity > 0)
                .ToList();
            var concessionIds = requestedItems.Select(c => c.ConcessionId).ToList();
            var concessions = await _db.Concessions
                .AsNoTracking()
                .Where(c => concessionIds.Contains(c.Id))
                .ToListAsync(cancellationToken);

            foreach (var requestedItem in requestedItems)
            {
                var concession = concessions.FirstOrDefault(c => c.Id == requestedItem.ConcessionId);
                if (concession == null || !concession.IsActive)
                {
                    throw new InvalidOperationException("Combo bắp nước không hợp lệ.");
                }

                concessionTotal += concession.Price * requestedItem.Quantity;
            }
        }

        var subtotal = seatTotal + concessionTotal;
        var discountAmount = await CalculatePromotionDiscountAsync(
            request.PromotionCode,
            subtotal,
            cancellationToken);
        var totalAfterPromotion = Math.Max(0, subtotal - discountAmount);
        var pointDiscountAmount = await CalculatePointDiscountAsync(
            userId,
            request.UsedPoints,
            totalAfterPromotion,
            cancellationToken);
        var effectiveUsedPoints = (int)pointDiscountAmount;

        return new BookingQuoteDto
        {
            SeatTotal = seatTotal,
            ConcessionTotal = concessionTotal,
            Subtotal = subtotal,
            PromotionCode = string.IsNullOrWhiteSpace(request.PromotionCode)
                ? null
                : request.PromotionCode.Trim(),
            DiscountAmount = discountAmount,
            UsedPoints = effectiveUsedPoints,
            PointDiscountAmount = pointDiscountAmount,
            TotalPrice = Math.Max(0, totalAfterPromotion - pointDiscountAmount)
        };
    }

    private async Task<decimal> CalculatePromotionDiscountAsync(
        string? promotionCode,
        decimal subtotal,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(promotionCode))
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        var normalizedCode = promotionCode.Trim().ToUpperInvariant();
        var promotion = await _db.Promotions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Code.ToUpper() == normalizedCode,
                cancellationToken);
        if (promotion == null)
        {
            throw new InvalidOperationException("Mã giảm giá không tồn tại.");
        }

        if (!string.Equals(promotion.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Mã giảm giá không còn hoạt động.");
        }

        if (promotion.StartDate > now || promotion.EndDate < now)
        {
            throw new InvalidOperationException("Mã giảm giá đã hết hạn hoặc chưa có hiệu lực.");
        }

        if (subtotal < promotion.MinOrder)
        {
            throw new InvalidOperationException("Đơn hàng chưa đạt giá trị tối thiểu để áp mã.");
        }

        var discount = string.Equals(promotion.DiscountType, "Percent", StringComparison.OrdinalIgnoreCase)
            || string.Equals(promotion.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase)
                ? subtotal * promotion.DiscountValue / 100m
                : promotion.DiscountValue;

        return Math.Min(subtotal, Math.Max(0, discount));
    }

    private async Task<decimal> CalculatePointDiscountAsync(
        Guid? userId,
        int usedPoints,
        decimal payableAmount,
        CancellationToken cancellationToken)
    {
        if (usedPoints <= 0)
        {
            return 0;
        }

        if (userId == null || userId == Guid.Empty)
        {
            throw new InvalidOperationException("Vui lòng đăng nhập để sử dụng điểm.");
        }

        var wallet = await _db.LoyaltyPoints
            .AsNoTracking()
            .FirstOrDefaultAsync(lp => lp.UserId == userId.Value, cancellationToken);
        var availablePoints = wallet?.Points ?? 0;
        if (usedPoints > availablePoints)
        {
            throw new InvalidOperationException("Số điểm sử dụng vượt quá số dư hiện có.");
        }

        return Math.Min(payableAmount, usedPoints * PointValue);
    }
}
