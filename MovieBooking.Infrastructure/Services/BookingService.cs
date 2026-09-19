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
using Microsoft.Extensions.Logging;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Exceptions;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Constants;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;
using Npgsql;

namespace MovieBooking.Infrastructure.Services;

public class BookingService : IBookingService
{
    private readonly EntityCrudOperations<Booking, BookingDto> _operations;
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPricingService _pricingService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        AppDbContext db,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IPricingService pricingService,
        TimeProvider timeProvider,
        ILogger<BookingService> logger)
    {
        _operations = new EntityCrudOperations<Booking, BookingDto>(db, mapper);
        _db = db;
        _mapper = mapper;
        _httpContextAccessor = httpContextAccessor;
        _pricingService = pricingService;
        _timeProvider = timeProvider;
        _logger = logger;
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
            .AsSplitQuery()
            .Include(b => b.Tickets)
                .ThenInclude(t => t.Seat)
            .Include(b => b.BookingConcessions)
                .ThenInclude(bc => bc.Concession)
            .Include(b => b.Showtime)
                .ThenInclude(s => s.Movie)
            .Include(b => b.Showtime)
                .ThenInclude(s => s.Room)
                    .ThenInclude(r => r.Cinema)
            .Include(b => b.User)
            .Include(b => b.Payment);
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
            .AsSplitQuery()
            .Include(b => b.Tickets)
                .ThenInclude(t => t.Seat)
            .Include(b => b.BookingConcessions)
                .ThenInclude(bc => bc.Concession)
            .Include(b => b.Showtime)
                .ThenInclude(s => s.Movie)
            .Include(b => b.Showtime)
                .ThenInclude(s => s.Room)
                    .ThenInclude(r => r.Cinema)
            .Include(b => b.User)
            .Include(b => b.Payment)
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
        return ExecuteWithRetryAsync(() => CreateInternalAsync(dto, false, cancellationToken), cancellationToken);
    }

    public Task<bool> UpdateAsync(Guid id, BookingDto dto, CancellationToken cancellationToken = default) =>
        _operations.UpdateAsync(id, dto, cancellationToken);

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.DeleteAsync(id, cancellationToken);

    public Task<BookingDto> CreatePointOfSaleAsync(BookingDto dto, CancellationToken cancellationToken = default)
    {
        return ExecuteWithRetryAsync(() => CreateInternalAsync(dto, true, cancellationToken), cancellationToken);
    }

    private async Task<BookingDto> ExecuteWithRetryAsync(
        Func<Task<BookingDto>> operation,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SeatHoldConflictException)
            {
                throw;
            }
            catch (Exception exception) when (IsRetryable(exception) && attempt < maxAttempts)
            {
                LogRetry(attempt, exception);
                _db.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
            catch (Exception exception) when (IsRetryable(exception))
            {
                LogRetry(attempt, exception);
                _db.ChangeTracker.Clear();
                throw new SeatHoldConflictException(
                    "The booking could not be created because availability changed.");
            }
        }

        throw new InvalidOperationException("Booking retry loop ended unexpectedly.");
    }

    private static bool IsRetryable(Exception exception)
    {
        var postgres = FindPostgresException(exception);
        return postgres?.SqlState is PostgresErrorCodes.SerializationFailure
            or PostgresErrorCodes.DeadlockDetected
            or PostgresErrorCodes.UniqueViolation;
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is PostgresException postgresException)
            {
                return postgresException;
            }
        }

        return null;
    }

    private void LogRetry(int attempt, Exception exception)
    {
        var postgres = FindPostgresException(exception);
        _logger.LogWarning(
            exception,
            "Booking transaction retry. Attempt={Attempt}, SqlState={SqlState}",
            attempt,
            postgres?.SqlState);
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
            throw new InvalidOperationException("Không tìm thấy suất chiếu.");
        }

        dto.SeatIds ??= new List<Guid>();
        if (dto.SeatIds.Count == 0 || !dto.SeatHoldGroupId.HasValue)
        {
            throw new InvalidOperationException("The seat list cannot be empty.");
        }

        // If UserId is not provided or is empty, assign current user ID
        var finalUserId = currentUserId;
        var bookingStatus = BookingStatuses.Pending;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var activeHoldRows = new List<SeatHold>();
        if (dto.SeatHoldGroupId.HasValue)
        {
            var existingBooking = await _db.Bookings
                .Include(b => b.Tickets)
                .Include(b => b.BookingConcessions)
                .FirstOrDefaultAsync(
                    b => b.SeatHoldGroupId == dto.SeatHoldGroupId.Value && b.UserId == finalUserId,
                    cancellationToken);

            if (existingBooking != null)
            {
                if (existingBooking.Status == BookingStatuses.Pending && existingBooking.ExpiredAt > now)
                {
                    var existingDto = _mapper.Map<BookingDto>(existingBooking);
                    existingDto.SeatIds = existingBooking.Tickets.Select(t => t.SeatId).ToList();
                    return existingDto;
                }

                if (existingBooking.Status == BookingStatuses.Paid)
                {
                    throw new InvalidOperationException("Đơn đặt vé cho lượt giữ chỗ này đã được thanh toán.");
                }
            }

            var candidateGroupSeatIds = await _db.SeatHolds
                .AsNoTracking()
                .Where(hold => hold.ShowtimeId == dto.ShowtimeId
                               && hold.UserId == finalUserId
                               && hold.HoldGroupId == dto.SeatHoldGroupId.Value
                               && hold.Status == SeatHoldStatuses.Active
                               && hold.ExpiredAt > now
                               && hold.BookingId == null)
                .OrderBy(hold => hold.SeatId)
                .Select(hold => hold.SeatId)
                .ToListAsync(cancellationToken);

            if (candidateGroupSeatIds.Count == 0)
            {
                throw new SeatHoldConflictException(
                    "An active seat hold owned by the current user is required.");
            }

            var groupSeatIds = candidateGroupSeatIds.OrderBy(id => id).ToArray();
            if (dto.SeatIds.Count > 0
                && !dto.SeatIds.OrderBy(id => id).SequenceEqual(groupSeatIds))
            {
                throw new SeatHoldConflictException(
                    "The requested seats do not match the active seat hold.");
            }

            dto.SeatIds = groupSeatIds.ToList();
        }

        // Lock the canonical group seats before re-reading mutable hold rows.
        var seats = await _db.Seats
            .FromSqlRaw(
                "SELECT * FROM \"Seats\" WHERE \"Id\" = ANY({0}) AND \"RoomId\" = {1} ORDER BY \"Id\" FOR UPDATE",
                dto.SeatIds.ToArray(),
                showtime.RoomId)
            .ToListAsync(cancellationToken);
        if (seats.Count != dto.SeatIds.Count)
        {
            throw new InvalidOperationException("Một hoặc nhiều ghế được chọn không hợp lệ hoặc không thuộc phòng chiếu này.");
        }

        activeHoldRows = await _db.SeatHolds
            .Where(hold => hold.ShowtimeId == dto.ShowtimeId
                           && hold.UserId == finalUserId
                           && hold.HoldGroupId == dto.SeatHoldGroupId!.Value
                           && hold.Status == SeatHoldStatuses.Active
                           && hold.ExpiredAt > now
                           && hold.BookingId == null)
            .OrderBy(hold => hold.SeatId)
            .ToListAsync(cancellationToken);
        if (activeHoldRows.Count != dto.SeatIds.Count
            || !activeHoldRows.Select(hold => hold.SeatId).SequenceEqual(dto.SeatIds.OrderBy(id => id)))
        {
            throw new SeatHoldConflictException(
                "The active seat hold changed while the booking was being created.");
        }

        // Check availability
        var purchasedSeatIds = await _db.Tickets
            .Include(t => t.Booking)
            .Where(t => t.Booking.ShowtimeId == dto.ShowtimeId
                        && t.Booking.Status == "Paid")
            .Select(t => t.SeatId)
            .ToListAsync(cancellationToken);

        var pendingSeatIds = await _db.Tickets
            .Include(t => t.Booking)
            .Where(t => t.Booking.ShowtimeId == dto.ShowtimeId
                        && t.Booking.Status == "Pending"
                        && t.Booking.ExpiredAt > now)
            .Select(t => t.SeatId)
            .ToListAsync(cancellationToken);

        var heldSeatIds = await _db.SeatHolds
            .Where(sh => sh.ShowtimeId == dto.ShowtimeId 
                         && sh.Status == SeatHoldStatuses.Active
                         && sh.ExpiredAt > now
                         && (!dto.SeatHoldGroupId.HasValue || sh.HoldGroupId != dto.SeatHoldGroupId.Value))
            .Select(sh => sh.SeatId)
            .ToListAsync(cancellationToken);

        foreach (var seatId in dto.SeatIds)
        {
            if (purchasedSeatIds.Contains(seatId))
            {
                throw new InvalidOperationException($"Ghế với ID {seatId} đã được đặt.");
            }
            if (pendingSeatIds.Contains(seatId) || heldSeatIds.Contains(seatId))
            {
                throw new InvalidOperationException($"Ghế với ID {seatId} đang được giữ bởi người dùng khác.");
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
                Status = TicketStatuses.Held,
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

        // Create booking
        var booking = new Booking
        {
            UserId = finalUserId,
            ShowtimeId = dto.ShowtimeId,
            SeatHoldGroupId = dto.SeatHoldGroupId,
            Channel = isPointOfSale ? BookingChannels.PointOfSale : BookingChannels.CustomerOnline,
            Status = bookingStatus,
            Subtotal = quote.Subtotal,
            DiscountAmount = 0,
            PointDiscountAmount = 0,
            UsedPoints = 0,
            TotalPrice = quote.TotalPrice,
            ExpiredAt = activeHoldRows[0].ExpiredAt,
            Tickets = tickets,
            BookingConcessions = bookingConcessions
        };

        await _db.Bookings.AddAsync(booking, cancellationToken);

        foreach (var hold in activeHoldRows)
        {
            hold.BookingId = booking.Id;
            hold.MarkUpdated(new DateTimeOffset(now, TimeSpan.Zero));
        }

        await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var resultDto = _mapper.Map<BookingDto>(booking);
        resultDto.SeatIds = dto.SeatIds; // Preserve seat IDs in result
        resultDto.PromotionCode = null;
        return resultDto;
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
                    .ThenInclude(s => s.Room)
                        .ThenInclude(r => r.Cinema)
            .Include(t => t.Booking)
                .ThenInclude(b => b.BookingConcessions)
                    .ThenInclude(bc => bc.Concession)
            .Include(t => t.Seat)
            .Where(t => t.Booking.UserId == userId)
            .OrderByDescending(t => t.Booking.Showtime.StartTime)
            .ToListAsync(cancellationToken);

        var result = new List<MyTicketDto>();
        foreach (var t in tickets)
        {
            result.Add(new MyTicketDto
            {
                Id = t.Id,
                BookingId = t.BookingId,
                MovieTitle = t.Booking.Showtime.Movie.Title,
                CinemaName = t.Booking.Showtime.Room.Cinema.Name,
                RoomName = t.Booking.Showtime.Room.Name,
                StartTime = t.Booking.Showtime.StartTime,
                SeatLabel = $"{t.Seat.RowLabel}{t.Seat.SeatNumber}",
                QrCode = t.QrCode,
                Status = t.Status,
                PaymentStatus = t.Booking.Status,
                Price = t.Price,
                Concessions = t.Booking.BookingConcessions.Select(bc => new TicketConcessionDto
                {
                    Name = bc.Concession.Name,
                    Quantity = bc.Quantity
                }).ToList()
            });
        }

        return result;
    }

    public async Task<List<MyTicketDto>> GetMySuccessfulTicketsAsync(CancellationToken cancellationToken = default)
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
                    .ThenInclude(s => s.Room)
                        .ThenInclude(r => r.Cinema)
            .Include(t => t.Booking)
                .ThenInclude(b => b.BookingConcessions)
                    .ThenInclude(bc => bc.Concession)
            .Include(t => t.Seat)
            .Where(t => t.Booking.UserId == userId 
                     && t.Status == TicketStatuses.Booked 
                     && t.Booking.Status == BookingStatuses.Paid)
            .OrderByDescending(t => t.Booking.Showtime.StartTime)
            .ToListAsync(cancellationToken);

        var result = new List<MyTicketDto>();
        foreach (var t in tickets)
        {
            result.Add(new MyTicketDto
            {
                Id = t.Id,
                BookingId = t.BookingId,
                MovieTitle = t.Booking.Showtime.Movie.Title,
                CinemaName = t.Booking.Showtime.Room.Cinema.Name,
                RoomName = t.Booking.Showtime.Room.Name,
                StartTime = t.Booking.Showtime.StartTime,
                SeatLabel = $"{t.Seat.RowLabel}{t.Seat.SeatNumber}",
                QrCode = t.QrCode,
                Status = t.Status,
                PaymentStatus = t.Booking.Status,
                Price = t.Price,
                Concessions = t.Booking.BookingConcessions.Select(bc => new TicketConcessionDto
                {
                    Name = bc.Concession.Name,
                    Quantity = bc.Quantity
                }).ToList()
            });
        }

        return result;
    }
}
