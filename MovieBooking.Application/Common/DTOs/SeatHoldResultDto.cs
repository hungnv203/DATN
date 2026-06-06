using System;

namespace MovieBooking.Application.Common.DTOs;

public class SeatHoldResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime? ExpiredAt { get; set; }
}
