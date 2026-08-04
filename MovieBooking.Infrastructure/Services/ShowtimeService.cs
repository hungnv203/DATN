using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Constants;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

public class ShowtimeService : IShowtimeService
{
    private readonly EntityCrudOperations<Showtime, ShowtimeDto> _operations;
    private readonly AppDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly TimeProvider _timeProvider;

    public ShowtimeService(AppDbContext dbContext, IMapper mapper, TimeProvider timeProvider)
    {
        _operations = new EntityCrudOperations<Showtime, ShowtimeDto>(dbContext, mapper);
        _dbContext = dbContext;
        _mapper = mapper;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<ShowtimeDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Showtimes
            .AsNoTracking()
            .Include(s => s.Room)
            .ToListAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<ShowtimeDto>>(entities);
    }

    public async Task<ShowtimeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Showtimes
            .AsNoTracking()
            .Include(s => s.Room)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        return entity is null ? null : _mapper.Map<ShowtimeDto>(entity);
    }

    public async Task<ShowtimeDto> CreateAsync(ShowtimeDto dto, CancellationToken cancellationToken = default)
    {
        await CheckClashAsync(dto, null, cancellationToken);
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var created = await _operations.CreateAsync(dto, cancellationToken);
        _dbContext.ShowtimeSeatVersions.Add(new ShowtimeSeatVersion
        {
            ShowtimeId = created.Id,
            Version = 0
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetByIdAsync(created.Id, cancellationToken) ?? created;
    }

    public async Task<bool> UpdateAsync(Guid id, ShowtimeDto dto, CancellationToken cancellationToken = default)
    {
        await CheckClashAsync(dto, id, cancellationToken);
        return await _operations.UpdateAsync(id, dto, cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.DeleteAsync(id, cancellationToken);

    private async Task CheckClashAsync(ShowtimeDto dto, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (dto.StartTime >= dto.EndTime)
        {
            throw new InvalidOperationException("Thời gian bắt đầu phải trước thời gian kết thúc.");
        }

        var movie = await _dbContext.Movies
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == dto.MovieId, cancellationToken);
        if (movie == null)
        {
            throw new InvalidOperationException("Movie not found.");
        }

        if (dto.StartTime.Date < movie.ReleaseDate.Date)
        {
            throw new InvalidOperationException("Showtime cannot be before the movie release date.");
        }

        var clashExists = await _dbContext.Showtimes
            .AnyAsync(s => s.RoomId == dto.RoomId 
                           && s.Id != excludeId
                           && dto.StartTime < s.EndTime 
                           && dto.EndTime > s.StartTime, 
                      cancellationToken);

        if (clashExists)
        {
            throw new InvalidOperationException("Xung đột suất chiếu: Phòng chiếu này đã có suất chiếu khác trong khoảng thời gian này.");
        }
    }

    public async Task<IReadOnlyList<ShowtimeSeatDto>> GetSeatsForShowtimeAsync(Guid showtimeId, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var showtime = await _dbContext.Showtimes
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == showtimeId, cancellationToken);

        if (showtime == null)
        {
            throw new KeyNotFoundException("Không tìm thấy suất chiếu.");
        }

        var seats = await _dbContext.Seats
            .AsNoTracking()
            .Where(s => s.RoomId == showtime.RoomId)
            .ToListAsync(cancellationToken);

        var purchasedSeatIds = await _dbContext.Tickets
            .AsNoTracking()
            .Include(t => t.Booking)
            .Where(t => t.Booking.ShowtimeId == showtimeId
                        && t.Booking.Status == "Paid")
            .Select(t => t.SeatId)
            .ToListAsync(cancellationToken);

        var purchasedSeatIdsSet = purchasedSeatIds.ToHashSet();

        var pendingSeats = await _dbContext.Tickets
            .AsNoTracking()
            .Include(t => t.Booking)
            .Where(t => t.Booking.ShowtimeId == showtimeId
                        && t.Booking.Status == "Pending"
                        && t.Booking.ExpiredAt > now)
            .Select(t => new { t.SeatId, t.Booking.UserId })
            .ToListAsync(cancellationToken);

        var activeHolds = await _dbContext.SeatHolds
            .AsNoTracking()
            .Where(sh => sh.ShowtimeId == showtimeId
                         && sh.Status == SeatHoldStatuses.Active
                         && sh.ExpiredAt > now)
            .ToListAsync(cancellationToken);

        var holdMap = activeHolds
            .Select(hold => new { hold.SeatId, hold.UserId })
            .Concat(pendingSeats)
            .GroupBy(hold => hold.SeatId)
            .ToDictionary(group => group.Key, group => group.First().UserId);

        var result = seats.Select(s => new ShowtimeSeatDto
        {
            SeatId = s.Id,
            RowLabel = s.RowLabel,
            SeatNumber = s.SeatNumber,
            Type = s.Type,
            Status = purchasedSeatIdsSet.Contains(s.Id)
                ? "Booked"
                : (holdMap.TryGetValue(s.Id, out var userId) ? "Held" : "Available"),
            HeldByUserId = holdMap.TryGetValue(s.Id, out var uId) ? uId : null
        }).OrderBy(s => s.RowLabel).ThenBy(s => s.SeatNumber).ToList();

        return result;
    }

    public async Task<SeatStateSnapshotDto> GetSeatStateAsync(
        Guid showtimeId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var legacySeats = await GetSeatsForShowtimeAsync(showtimeId, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var holds = await _dbContext.SeatHolds
            .AsNoTracking()
            .Where(hold => hold.ShowtimeId == showtimeId
                           && hold.Status == SeatHoldStatuses.Active
                           && hold.ExpiredAt > now)
            .Select(hold => new { hold.SeatId, hold.UserId, hold.ExpiredAt })
            .ToListAsync(cancellationToken);
        var holdMap = holds.GroupBy(hold => hold.SeatId)
            .ToDictionary(group => group.Key, group => group.First());
        var version = await _dbContext.ShowtimeSeatVersions
            .AsNoTracking()
            .Where(item => item.ShowtimeId == showtimeId)
            .Select(item => (long?)item.Version)
            .SingleOrDefaultAsync(cancellationToken) ?? 0;

        return new SeatStateSnapshotDto
        {
            Version = version,
            Seats = legacySeats.Select(seat =>
            {
                holdMap.TryGetValue(seat.SeatId, out var hold);
                return new RealtimeShowtimeSeatDto
                {
                    SeatId = seat.SeatId,
                    RowLabel = seat.RowLabel,
                    SeatNumber = seat.SeatNumber,
                    Type = seat.Type,
                    Status = seat.Status,
                    ExpiresAtUtc = hold?.ExpiredAt,
                    HeldByCurrentUser = hold?.UserId == userId
                };
            }).ToArray()
        };
    }
}
