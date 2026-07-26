using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Application.Common.Interfaces;

public interface IShowtimeService : ICrudService<Showtime, ShowtimeDto>
{
    Task<IReadOnlyList<ShowtimeSeatDto>> GetSeatsForShowtimeAsync(
        Guid showtimeId,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);
    Task<ShowtimeSeatDto?> GetSeatForShowtimeAsync(
        Guid showtimeId,
        Guid seatId,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);
}
