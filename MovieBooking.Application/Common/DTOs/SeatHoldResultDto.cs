using System;

namespace MovieBooking.Application.Common.DTOs;

public class SeatHoldResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? HoldSessionId { get; set; }
    public DateTime ServerTime { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public string Status { get; set; } = string.Empty;
}
