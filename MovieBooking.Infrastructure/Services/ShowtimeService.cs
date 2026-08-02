using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

public class ShowtimeService : IShowtimeService
{
    private readonly EntityCrudOperations<Showtime, ShowtimeDto> _operations;
    private readonly AppDbContext _dbContext;
    private readonly IMapper _mapper;

    public ShowtimeService(AppDbContext dbContext, IMapper mapper)
    {
        _operations = new EntityCrudOperations<Showtime, ShowtimeDto>(dbContext, mapper);
        _dbContext = dbContext;
        _mapper = mapper;
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
        var created = await _operations.CreateAsync(dto, cancellationToken);
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
            throw new InvalidOperationException("ThÃ¡Â»Âi gian bÃ¡ÂºÂ¯t Ã„â€˜Ã¡ÂºÂ§u phÃ¡ÂºÂ£i trÃ†Â°Ã¡Â»â€ºc thÃ¡Â»Âi gian kÃ¡ÂºÂ¿t thÃƒÂºc.");
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
            throw new InvalidOperationException("Xung Ã„â€˜Ã¡Â»â„¢t suÃ¡ÂºÂ¥t chiÃ¡ÂºÂ¿u: PhÃƒÂ²ng chiÃ¡ÂºÂ¿u nÃƒÂ y Ã„â€˜ÃƒÂ£ cÃƒÂ³ suÃ¡ÂºÂ¥t chiÃ¡ÂºÂ¿u khÃƒÂ¡c trong khoÃ¡ÂºÂ£ng thÃ¡Â»Âi gian nÃƒÂ y.");
        }
    }

    public async Task<IReadOnlyList<ShowtimeSeatDto>> GetSeatsForShowtimeAsync(Guid showtimeId, CancellationToken cancellationToken = default)
    {
        var showtime = await _dbContext.Showtimes
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == showtimeId, cancellationToken);

        if (showtime == null)
        {
            throw new KeyNotFoundException("KhÃƒÂ´ng tÃƒÂ¬m thÃ¡ÂºÂ¥y suÃ¡ÂºÂ¥t chiÃ¡ÂºÂ¿u.");
        }

        var seats = await _dbContext.Seats
            .AsNoTracking()
            .Where(s => s.RoomId == showtime.RoomId)
            .ToListAsync(cancellationToken);

        var reservedSeatIds = await _dbContext.Tickets
            .AsNoTracking()
            .Include(t => t.Booking)
            .Where(t => t.Booking.ShowtimeId == showtimeId 
                        && t.Booking.Status != "Cancelled" 
                        && t.Booking.Status != "Expired")
            .Select(t => t.SeatId)
            .ToListAsync(cancellationToken);

        var reservedSeatIdsSet = reservedSeatIds.ToHashSet();

        var activeHolds = await _dbContext.SeatHolds
            .AsNoTracking()
            .Where(sh => sh.ShowtimeId == showtimeId && sh.ExpiredAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        var holdMap = activeHolds.ToDictionary(sh => sh.SeatId, sh => sh.UserId);

        var result = seats.Select(s => new ShowtimeSeatDto
        {
            SeatId = s.Id,
            RowLabel = s.RowLabel,
            SeatNumber = s.SeatNumber,
            Type = s.Type,
            Status = reservedSeatIdsSet.Contains(s.Id) 
                ? "Reserved" 
                : (holdMap.TryGetValue(s.Id, out var userId) ? "Held" : "Available"),
            HeldByUserId = holdMap.TryGetValue(s.Id, out var uId) ? uId : null
        }).OrderBy(s => s.RowLabel).ThenBy(s => s.SeatNumber).ToList();

        return result;
    }
}

