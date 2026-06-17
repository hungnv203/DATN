using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

public class TicketCrudService : EfCrudService<Ticket, TicketDto>
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TicketCrudService(AppDbContext db, IMapper mapper, IHttpContextAccessor httpContextAccessor)
        : base(db, mapper)
    {
        _db = db;
        _mapper = mapper;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<IReadOnlyList<TicketDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return Array.Empty<TicketDto>();
        }

        var user = httpContext.User;
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Array.Empty<TicketDto>();
        }

        var isAdminOrManager = user.IsInRole("Admin") || user.IsInRole("Manager") || user.IsInRole("Cashier");

        IQueryable<Ticket> query = _db.Tickets
            .Include(t => t.Seat)
            .Include(t => t.Booking)
                .ThenInclude(b => b.Showtime)
                    .ThenInclude(s => s.Movie);
        if (!isAdminOrManager)
        {
            query = query.Where(t => t.Booking.UserId == userId);
        }

        var tickets = await query.AsNoTracking().ToListAsync(cancellationToken);
        return tickets.Select(t => _mapper.Map<TicketDto>(t)).ToList();
    }

    public override async Task<TicketDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Seat)
            .Include(t => t.Booking)
                .ThenInclude(b => b.Showtime)
                    .ThenInclude(s => s.Movie)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (ticket == null) return null;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            var user = httpContext.User;
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub");
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                var isAdminOrManager = user.IsInRole("Admin") || user.IsInRole("Manager") || user.IsInRole("Cashier");
                if (!isAdminOrManager && ticket.Booking.UserId != userId)
                {
                    return null;
                }
            }
        }

        return _mapper.Map<TicketDto>(ticket);
    }
}
