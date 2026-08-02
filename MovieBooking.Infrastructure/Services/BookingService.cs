using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

public class BookingService : IBookingService
{
    private readonly EntityCrudOperations<Booking, BookingDto> _operations;
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPricingService _pricingService;
    private readonly ILoyaltyService _loyaltyService;

    public BookingService(
        AppDbContext db,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IPricingService pricingService,
        ILoyaltyService loyaltyService)
    {
        _operations = new EntityCrudOperations<Booking, BookingDto>(db, mapper);
        _db = db;
        _mapper = mapper;
        _httpContextAccessor = httpContextAccessor;
        _pricingService = pricingService;
        _loyaltyService = loyaltyService;
    }

    public async Task<IReadOnlyList<BookingDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return Array.Empty<BookingDto>();
        }

        var user = httpContext.User;
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Array.Empty<BookingDto>();
        }

        var isAdminOrManager = user.IsInRole("Admin") || user.IsInRole("Manager") || user.IsInRole("Cashier");

        IQueryable<Booking> query = _db.Bookings
            .Include(b => b.Tickets)
            .Include(b => b.BookingConcessions)
                .ThenInclude(bc => bc.Concession);
        if (!isAdminOrManager)
        {
            query = query.Where(b => b.UserId == userId);
        }

        var bookings = await query
            .AsNoTracking()
            .OrderByDescending(booking => booking.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        return bookings.Select(b => _mapper.Map<BookingDto>(b)).ToList();
    }

    public async Task<BookingDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var booking = await _db.Bookings
            .Include(b => b.Tickets)
            .Include(b => b.BookingConcessions)
                .ThenInclude(bc => bc.Concession)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (booking == null) return null;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            var user = httpContext.User;
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub");
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                var isAdminOrManager = user.IsInRole("Admin") || user.IsInRole("Manager") || user.IsInRole("Cashier");
                if (!isAdminOrManager && booking.UserId != userId)
                {
                    return null;
                }
            }
        }

        return _mapper.Map<BookingDto>(booking);
    }

    public Task<BookingDto> CreateAsync(BookingDto dto, CancellationToken cancellationToken = default)
    {
        return CreateInternalAsync(dto, false, cancellationToken);
    }

    public Task<bool> UpdateAsync(Guid id, BookingDto dto, CancellationToken cancellationToken = default) =>
        _operations.UpdateAsync(id, dto, cancellationToken);

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.DeleteAsync(id, cancellationToken);

    public Task<BookingDto> CreatePointOfSaleAsync(BookingDto dto, CancellationToken cancellationToken = default)
    {
        return CreateInternalAsync(dto, true, cancellationToken);
    }

    private async Task<BookingDto> CreateInternalAsync(
        BookingDto dto,
        bool isPointOfSale,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        Guid currentUserId = Guid.Empty;
        if (httpContext != null)
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier) ?? httpContext.User.FindFirst("sub");
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var parsedId))
            {
                currentUserId = parsedId;
            }
        }

        if (currentUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authentication is required to create a booking.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        // Validate showtime
        var showtime = await _db.Showtimes.Include(s => s.Room).FirstOrDefaultAsync(s => s.Id == dto.ShowtimeId, cancellationToken);
        if (showtime == null)
        {
            throw new InvalidOperationException("KhÃƒÂ´ng tÃƒÂ¬m thÃ¡ÂºÂ¥y suÃ¡ÂºÂ¥t chiÃ¡ÂºÂ¿u.");
        }

        if (dto.SeatIds == null || dto.SeatIds.Count == 0)
        {
            throw new InvalidOperationException("Danh sÃƒÂ¡ch ghÃ¡ÂºÂ¿ khÃƒÂ´ng Ã„â€˜Ã†Â°Ã¡Â»Â£c Ã„â€˜Ã¡Â»Æ’ trÃ¡Â»â€˜ng.");
        }

        // If UserId is not provided or is empty, assign current user ID
        var finalUserId = isPointOfSale && dto.UserId != Guid.Empty
            ? dto.UserId
            : currentUserId;
        var bookingStatus = isPointOfSale ? "Paid" : "Pending";

        // Fetch all seats
        var seats = await _db.Seats.Where(s => dto.SeatIds.Contains(s.Id) && s.RoomId == showtime.RoomId).ToListAsync(cancellationToken);
        if (seats.Count != dto.SeatIds.Count)
        {
            throw new InvalidOperationException("MÃ¡Â»â„¢t hoÃ¡ÂºÂ·c nhiÃ¡Â»Âu ghÃ¡ÂºÂ¿ Ã„â€˜Ã†Â°Ã¡Â»Â£c chÃ¡Â»Ân khÃƒÂ´ng hÃ¡Â»Â£p lÃ¡Â»â€¡ hoÃ¡ÂºÂ·c khÃƒÂ´ng thuÃ¡Â»â„¢c phÃƒÂ²ng chiÃ¡ÂºÂ¿u nÃƒÂ y.");
        }

        // Check availability
        var reservedSeatIds = await _db.Tickets
            .Include(t => t.Booking)
            .Where(t => t.Booking.ShowtimeId == dto.ShowtimeId 
                        && t.Booking.Status != "Cancelled" 
                        && t.Booking.Status != "Expired")
            .Select(t => t.SeatId)
            .ToListAsync(cancellationToken);

        var heldSeatIds = await _db.SeatHolds
            .Where(sh => sh.ShowtimeId == dto.ShowtimeId 
                         && sh.ExpiredAt > DateTime.UtcNow 
                         && sh.UserId != finalUserId)
            .Select(sh => sh.SeatId)
            .ToListAsync(cancellationToken);

        foreach (var seatId in dto.SeatIds)
        {
            if (reservedSeatIds.Contains(seatId))
            {
                throw new InvalidOperationException($"GhÃ¡ÂºÂ¿ vÃ¡Â»â€ºi ID {seatId} Ã„â€˜ÃƒÂ£ Ã„â€˜Ã†Â°Ã¡Â»Â£c Ã„â€˜Ã¡ÂºÂ·t.");
            }
            if (heldSeatIds.Contains(seatId))
            {
                throw new InvalidOperationException($"GhÃ¡ÂºÂ¿ vÃ¡Â»â€ºi ID {seatId} Ã„â€˜ang Ã„â€˜Ã†Â°Ã¡Â»Â£c giÃ¡Â»Â¯ bÃ¡Â»Å¸i ngÃ†Â°Ã¡Â»Âi dÃƒÂ¹ng khÃƒÂ¡c.");
            }
        }

        var quote = await _pricingService.QuoteAsync(
            new BookingQuoteRequestDto
            {
                ShowtimeId = dto.ShowtimeId,
                SeatIds = dto.SeatIds,
                Concessions = dto.Concessions,
                PromotionCode = dto.PromotionCode,
                UsedPoints = dto.UsedPoints
            },
            finalUserId,
            cancellationToken);

        var tickets = new List<Ticket>();
        foreach (var seat in seats)
        {
            decimal price = showtime.BasePrice;
            if (seat.Type == "VIP")
            {
                price += 20000; // VIP markup
            }
            else if (seat.Type == "Couple")
            {
                price += 40000; // Couple markup
            }
            tickets.Add(new Ticket
            {
                SeatId = seat.Id,
                Price = price,
                Status = "Reserved",
                QrCode = Guid.NewGuid().ToString("N") // Temporary QR Code content
            });
        }

        var bookingConcessions = new List<BookingConcession>();
        if (dto.Concessions != null && dto.Concessions.Count > 0)
        {
            var concessionIds = dto.Concessions.Select(c => c.ConcessionId).ToList();
            var concessions = await _db.Concessions.Where(c => concessionIds.Contains(c.Id)).ToListAsync(cancellationToken);
            foreach (var reqConc in dto.Concessions)
            {
                var concession = concessions.FirstOrDefault(c => c.Id == reqConc.ConcessionId);
                if (concession != null)
                {
                    bookingConcessions.Add(new BookingConcession
                    {
                        ConcessionId = concession.Id,
                        Concession = concession,
                        Quantity = reqConc.Quantity,
                        Price = concession.Price
                    });
                }
            }
        }

        var bookingPromotions = new List<BookingPromotion>();
        if (!string.IsNullOrWhiteSpace(dto.PromotionCode) && quote.DiscountAmount > 0)
        {
            var normalizedCode = dto.PromotionCode.Trim().ToUpperInvariant();
            var promotion = await _db.Promotions.FirstOrDefaultAsync(
                p => p.Code.ToUpper() == normalizedCode,
                cancellationToken);
            if (promotion != null)
            {
                bookingPromotions.Add(new BookingPromotion
                {
                    PromotionId = promotion.Id,
                    Promotion = promotion,
                    DiscountAmount = quote.DiscountAmount
                });
            }
        }

        // Create booking
        var booking = new Booking
        {
            UserId = finalUserId,
            ShowtimeId = dto.ShowtimeId,
            Status = bookingStatus,
            Subtotal = quote.Subtotal,
            DiscountAmount = quote.DiscountAmount,
            PointDiscountAmount = quote.PointDiscountAmount,
            UsedPoints = quote.UsedPoints,
            TotalPrice = quote.TotalPrice,
            ExpiredAt = isPointOfSale ? null : DateTime.UtcNow.AddMinutes(5),
            Tickets = tickets,
            BookingConcessions = bookingConcessions,
            BookingPromotions = bookingPromotions
        };

        await _db.Bookings.AddAsync(booking, cancellationToken);

        // Delete any existing holds of this user for these seats
        var holdsToRemove = await _db.SeatHolds
            .Where(sh => sh.ShowtimeId == dto.ShowtimeId && sh.UserId == finalUserId && dto.SeatIds.Contains(sh.SeatId))
            .ToListAsync(cancellationToken);
        if (holdsToRemove.Any())
        {
            _db.SeatHolds.RemoveRange(holdsToRemove);
        }

        await _db.SaveChangesAsync(cancellationToken);
        if (quote.UsedPoints > 0)
        {
            await _loyaltyService.RedeemForBookingAsync(booking.Id, quote.UsedPoints, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var resultDto = _mapper.Map<BookingDto>(booking);
        resultDto.SeatIds = dto.SeatIds; // Preserve seat IDs in result
        resultDto.PromotionCode = dto.PromotionCode;
        return resultDto;
    }

    public async Task<SeatHoldResultDto> HoldSeatsAsync(HoldSeatsRequestDto request, CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return new SeatHoldResultDto { Success = false, Message = "YÃƒÂªu cÃ¡ÂºÂ§u khÃƒÂ´ng hÃ¡Â»Â£p lÃ¡Â»â€¡." };
        }

        var user = httpContext.User;
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return new SeatHoldResultDto { Success = false, Message = "Vui lÃƒÂ²ng Ã„â€˜Ã„Æ’ng nhÃ¡ÂºÂ­p Ã„â€˜Ã¡Â»Æ’ giÃ¡Â»Â¯ ghÃ¡ÂºÂ¿." };
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        // Validate showtime
        var showtimeExists = await _db.Showtimes.AnyAsync(s => s.Id == request.ShowtimeId, cancellationToken);
        if (!showtimeExists)
        {
            return new SeatHoldResultDto { Success = false, Message = "KhÃƒÂ´ng tÃƒÂ¬m thÃ¡ÂºÂ¥y suÃ¡ÂºÂ¥t chiÃ¡ÂºÂ¿u." };
        }

        if (request.SeatIds == null || request.SeatIds.Count == 0)
        {
            return new SeatHoldResultDto { Success = false, Message = "Danh sÃƒÂ¡ch ghÃ¡ÂºÂ¿ trÃ¡Â»â€˜ng." };
        }

        // Check if seats are already reserved
        var reservedSeatIds = await _db.Tickets
            .Include(t => t.Booking)
            .Where(t => t.Booking.ShowtimeId == request.ShowtimeId 
                        && t.Booking.Status != "Cancelled" 
                        && t.Booking.Status != "Expired")
            .Select(t => t.SeatId)
            .ToListAsync(cancellationToken);

        // Check if seats are already held by OTHER users
        var heldSeatIds = await _db.SeatHolds
            .Where(sh => sh.ShowtimeId == request.ShowtimeId 
                         && sh.ExpiredAt > DateTime.UtcNow 
                         && sh.UserId != userId)
            .Select(sh => sh.SeatId)
            .ToListAsync(cancellationToken);

        foreach (var seatId in request.SeatIds)
        {
            if (reservedSeatIds.Contains(seatId))
            {
                return new SeatHoldResultDto { Success = false, Message = $"GhÃ¡ÂºÂ¿ Ã„â€˜ÃƒÂ£ Ã„â€˜Ã†Â°Ã¡Â»Â£c Ã„â€˜Ã¡ÂºÂ·t." };
            }
            if (heldSeatIds.Contains(seatId))
            {
                return new SeatHoldResultDto { Success = false, Message = $"GhÃ¡ÂºÂ¿ Ã„â€˜ang Ã„â€˜Ã†Â°Ã¡Â»Â£c giÃ¡Â»Â¯ bÃ¡Â»Å¸i ngÃ†Â°Ã¡Â»Âi khÃƒÂ¡c." };
            }
        }

        // Release user's previous holds for this showtime
        var existingHolds = await _db.SeatHolds
            .Where(sh => sh.ShowtimeId == request.ShowtimeId && sh.UserId == userId)
            .ToListAsync(cancellationToken);
        if (existingHolds.Any())
        {
            _db.SeatHolds.RemoveRange(existingHolds);
        }

        // Create new holds
        var expiry = DateTime.UtcNow.AddMinutes(5);
        var newHolds = request.SeatIds.Select(seatId => new SeatHold
        {
            ShowtimeId = request.ShowtimeId,
            SeatId = seatId,
            UserId = userId,
            ExpiredAt = expiry
        }).ToList();

        await _db.SeatHolds.AddRangeAsync(newHolds, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SeatHoldResultDto
        {
            Success = true,
            Message = "GiÃ¡Â»Â¯ ghÃ¡ÂºÂ¿ thÃƒÂ nh cÃƒÂ´ng.",
            ExpiredAt = expiry
        };
    }

    public async Task<List<MyTicketDto>> GetMyTicketsAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return new List<MyTicketDto>();

        var user = httpContext.User;
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return new List<MyTicketDto>();

        var tickets = await _db.Tickets
            .Include(t => t.Booking)
                .ThenInclude(b => b.Showtime)
                    .ThenInclude(s => s.Movie)
            .Include(t => t.Booking)
                .ThenInclude(b => b.Showtime)
       …22834 tokens truncated…ending(r => r.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        return reviews.Select(r => _mapper.Map<MovieReviewDto>(r)).ToList();
    }

    public async Task<IReadOnlyList<MovieReviewDto>> GetVisibleReviewsAsync(
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var reviews = await _db.MovieReviews
            .AsNoTracking()
            .Include(r => r.User)
            .Where(r => r.MovieId == movieId && r.Status == "Visible")
            .OrderByDescending(r => r.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        return reviews.Select(r => _mapper.Map<MovieReviewDto>(r)).ToList();
    }

    public async Task<MovieRatingSummaryDto> GetRatingSummaryAsync(
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var ratings = await _db.MovieReviews
            .AsNoTracking()
            .Where(r => r.MovieId == movieId && r.Status == "Visible")
            .Select(r => r.Rating)
            .ToListAsync(cancellationToken);

        return new MovieRatingSummaryDto
        {
            MovieId = movieId,
            AverageRating = ratings.Count == 0 ? 0 : Math.Round(ratings.Average(), 1),
            TotalReviews = ratings.Count,
            RatingBreakdown = Enumerable.Range(1, 5).ToDictionary(
                rating => rating,
                rating => ratings.Count(value => value == rating))
        };
    }

    public async Task<MovieReviewDto> CreateReviewAsync(
        Guid movieId,
        CreateMovieReviewDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.Rating < 1 || dto.Rating > 5)
        {
            throw new InvalidOperationException("Rating pháº£i náº±m trong khoáº£ng 1 Ä‘áº¿n 5.");
        }

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException("Vui lÃ²ng Ä‘Äƒng nháº­p Ä‘á»ƒ Ä‘Ã¡nh giÃ¡ phim.");
        }

        var alreadyReviewed = await _db.MovieReviews.AnyAsync(
            r => r.MovieId == movieId && r.UserId == userId,
            cancellationToken);
        if (alreadyReviewed)
        {
            throw new InvalidOperationException("Báº¡n Ä‘Ã£ Ä‘Ã¡nh giÃ¡ phim nÃ y.");
        }

        var eligibleBooking = await _db.Bookings
            .Include(b => b.Showtime)
            .Where(b => b.UserId == userId
                && b.Status == "Paid"
                && b.Showtime.MovieId == movieId
                && b.Showtime.StartTime <= DateTime.UtcNow)
            .OrderByDescending(b => b.Showtime.StartTime)
            .FirstOrDefaultAsync(cancellationToken);
        if (eligibleBooking == null)
        {
            throw new InvalidOperationException("Báº¡n chá»‰ cÃ³ thá»ƒ Ä‘Ã¡nh giÃ¡ phim Ä‘Ã£ mua vÃ© vÃ  Ä‘Ã£ xem.");
        }

        var review = new MovieReview
        {
            MovieId = movieId,
            UserId = userId,
            BookingId = eligibleBooking.Id,
            Rating = dto.Rating,
            Comment = dto.Comment.Trim(),
            Status = "Visible"
        };

        _db.MovieReviews.Add(review);
        await _db.SaveChangesAsync(cancellationToken);

        await _db.Entry(review).Reference(r => r.User).LoadAsync(cancellationToken);
        return _mapper.Map<MovieReviewDto>(review);
    }

    public async Task<bool> HideReviewAsync(Guid reviewId, CancellationToken cancellationToken = default)
    {
        var review = await _db.MovieReviews.FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken);
        if (review == null)
        {
            return false;
        }

        review.Status = "Hidden";
        review.MarkUpdated(DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private Guid GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var claim = user?.FindFirst(ClaimTypes.NameIdentifier) ?? user?.FindFirst("sub");
        return claim != null && Guid.TryParse(claim.Value, out var userId) ? userId : Guid.Empty;
    }
}

