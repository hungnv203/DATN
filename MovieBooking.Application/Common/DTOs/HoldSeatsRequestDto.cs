using System;
using System.Collections.Generic;

namespace MovieBooking.Application.Common.DTOs;

public class HoldSeatsRequestDto
{
    public Guid ShowtimeId { get; set; }
    public List<Guid> SeatIds { get; set; } = new();
}
