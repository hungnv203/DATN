using System;

namespace MovieBooking.Application.Common.DTOs;

public class SeatHoldResultDto
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? HoldGroupId { get; set; }
    public Guid? ShowtimeId { get; set; }
    public IReadOnlyList<Guid> SeatIds { get; set; } = Array.Empty<Guid>();
    public string? Status { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public DateTime ServerTimeUtc { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public SeatStateChangeBatchDto? ChangeBatch { get; set; }
}
