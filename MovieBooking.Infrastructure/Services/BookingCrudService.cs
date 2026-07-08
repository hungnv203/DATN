using System;
using System.Collections.Generic;
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

public class BookingCrudService : EfCrudService<Booking, BookingDto>, IBookingService
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BookingCrudService(AppDbContext db, IMapper mapper, IHttpContextAccessor httpContextAccessor)
        : base(db, mapper)
    {
        _db = db;
        _mapper = mapper;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<IReadOnlyList<BookingDto>> GetAllAsync(CancellationToken cancellationToken = default)
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

        var bookings = await query.AsNoTracking().ToListAsync(cancellationToken);
        return bookings.Select(b => _mapper.Map<BookingDto>(b)).ToList();
    }

    public override async Task<BookingDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
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

    public override async Task<BookingDto> CreateAsync(BookingDto dto, CancellationToken cancellationToken = default)
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

        // Validate showtime
        var showtime = await _db.Showtimes.Include(s => s.Room).FirstOrDefaultAsync(s => s.Id == dto.ShowtimeId, cancellationToken);
        if (showtime == null)
        {
            throw new InvalidOperationException("Không tìm thấy suất chiếu.");
        }

        if (dto.SeatIds == null || dto.SeatIds.Count == 0)
        {
            throw new InvalidOperationException("Danh sách ghế không được để trống.");
        }

        // If UserId is not provided or is empty, assign current user ID
        var finalUserId = dto.UserId == Guid.Empty ? currentUserId : dto.UserId;
        if (finalUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Vui lòng đăng nhập để đặt vé.");
        }

        // Fetch all seats
        var seats = await _db.Seats.Where(s => dto.SeatIds.Contains(s.Id) && s.RoomId == showtime.RoomId).ToListAsync(cancellationToken);
        if (seats.Count != dto.SeatIds.Count)
        {
            throw new InvalidOperationException("Một hoặc nhiều ghế được chọn không hợp lệ hoặc không thuộc phòng chiếu này.");
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
                throw new InvalidOperationException($"Ghế với ID {seatId} đã được đặt.");
            }
            if (heldSeatIds.Contains(seatId))
            {
                throw new InvalidOperationException($"Ghế với ID {seatId} đang được giữ bởi người dùng khác.");
            }
        }

        // Calculate pricing
        decimal total = 0;
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
            total += price;

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
                    total += concession.Price * reqConc.Quantity;
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
            Status = dto.Status ?? "Pending",
            TotalPrice = total,
            ExpiredAt = (dto.Status == "Paid") ? null : DateTime.UtcNow.AddMinutes(5), // If paid (POS), no expiration. If pending (Online), expires in 5 mins
            Tickets = tickets,
            BookingConcessions = bookingConcessions
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

        var resultDto = _mapper.Map<BookingDto>(booking);
        resultDto.SeatIds = dto.SeatIds; // Preserve seat IDs in result
        return resultDto;
    }

    public async Task<SeatHoldResultDto> HoldSeatsAsync(HoldSeatsRequestDto request, CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return new SeatHoldResultDto { Success = false, Message = "Yêu cầu không hợp lệ." };
        }

        var user = httpContext.User;
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return new SeatHoldResultDto { Success = false, Message = "Vui lòng đăng nhập để giữ ghế." };
        }

        // Validate showtime
        var showtimeExists = await _db.Showtimes.AnyAsync(s => s.Id == request.ShowtimeId, cancellationToken);
        if (!showtimeExists)
        {
            return new SeatHoldResultDto { Success = false, Message = "Không tìm thấy suất chiếu." };
        }

        if (request.SeatIds == null || request.SeatIds.Count == 0)
        {
            return new SeatHoldResultDto { Success = false, Message = "Danh sách ghế trống." };
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
                return new SeatHoldResultDto { Success = false, Message = $"Ghế đã được đặt." };
            }
            if (heldSeatIds.Contains(seatId))
            {
                return new SeatHoldResultDto { Success = false, Message = $"Ghế đang được giữ bởi người khác." };
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

        return new SeatHoldResultDto
        {
            Success = true,
            Message = "Giữ ghế thành công.",
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
}
