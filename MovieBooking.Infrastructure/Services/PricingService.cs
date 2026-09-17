using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
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

        return new BookingQuoteDto
        {
            SeatTotal = seatTotal,
            ConcessionTotal = concessionTotal,
            Subtotal = subtotal,
            PromotionCode = null,
            DiscountAmount = 0,
            UsedPoints = 0,
            PointDiscountAmount = 0,
            TotalPrice = subtotal
        };
    }
}
