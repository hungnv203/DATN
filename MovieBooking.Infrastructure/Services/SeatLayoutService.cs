using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

public class SeatLayoutService : ISeatLayoutService
{
    private static readonly HashSet<string> SupportedSeatTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Standard",
            "VIP",
            "Couple"
        };

    private readonly AppDbContext _db;

    public SeatLayoutService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SeatDto>> CreateBulkAsync(
        BulkSeatLayoutDto layout,
        CancellationToken cancellationToken = default)
    {
        if (layout.RoomId == Guid.Empty)
        {
            throw new ArgumentException("RoomId is required.");
        }

        if (layout.Seats.Count is < 1 or > 500)
        {
            throw new ArgumentException("A seat layout must contain between 1 and 500 seats.");
        }

        var roomExists = await _db.Rooms
            .AsNoTracking()
            .AnyAsync(room => room.Id == layout.RoomId, cancellationToken);
        if (!roomExists)
        {
            throw new KeyNotFoundException("Room not found.");
        }

        var roomAlreadyHasSeats = await _db.Seats
            .AsNoTracking()
            .AnyAsync(seat => seat.RoomId == layout.RoomId, cancellationToken);
        if (roomAlreadyHasSeats)
        {
            throw new InvalidOperationException("The room already has a seat layout.");
        }

        var normalizedSeats = layout.Seats.Select(item => new
        {
            RowLabel = item.RowLabel.Trim().ToUpperInvariant(),
            item.SeatNumber,
            Type = item.Type.Trim()
        }).ToList();

        if (normalizedSeats.Any(item =>
                item.RowLabel.Length is < 1 or > 4
                || item.SeatNumber <= 0
                || !SupportedSeatTypes.Contains(item.Type)))
        {
            throw new ArgumentException("One or more seats have invalid row, number, or type.");
        }

        var uniqueSeatCount = normalizedSeats
            .Select(item => (item.RowLabel, item.SeatNumber))
            .Distinct()
            .Count();
        if (uniqueSeatCount != normalizedSeats.Count)
        {
            throw new ArgumentException("The seat layout contains duplicate positions.");
        }

        var seats = normalizedSeats.Select(item => new Seat
        {
            RoomId = layout.RoomId,
            RowLabel = item.RowLabel,
            SeatNumber = item.SeatNumber,
            Type = item.Type
        }).ToList();

        await _db.Seats.AddRangeAsync(seats, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return seats.Select(seat => new SeatDto
        {
            Id = seat.Id,
            RoomId = seat.RoomId,
            RowLabel = seat.RowLabel,
            SeatNumber = seat.SeatNumber,
            Type = seat.Type
        }).ToList();
    }
}
