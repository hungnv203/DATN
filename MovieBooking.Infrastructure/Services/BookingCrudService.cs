using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

public class BookingCrudService : EfCrudService<Booking, BookingDto>
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

        IQueryable<Booking> query = _db.Bookings;
        if (!isAdminOrManager)
        {
            query = query.Where(b => b.UserId == userId);
        }

        var bookings = await query.AsNoTracking().ToListAsync(cancellationToken);
        return bookings.Select(b => _mapper.Map<BookingDto>(b)).ToList();
    }

    public override async Task<BookingDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var booking = await _db.Bookings.FindAsync([id], cancellationToken);
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
}
