using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Application.Common.Interfaces;

public interface IShowtimeService : ICrudService<Showtime, ShowtimeDto>
{
    Task<IReadOnlyList<ShowtimeSeatDto>> GetSeatsForShowtimeAsync(Guid showtimeId, CancellationToken cancellationToken = default);
}
