namespace MovieBooking.Application.Common.DTOs;

public sealed class SeatStateSnapshotDto
{
    public long Version { get; init; }
    public IReadOnlyList<RealtimeShowtimeSeatDto> Seats { get; init; } = [];
}

public sealed class RealtimeShowtimeSeatDto
{
    public Guid SeatId { get; init; }
    public string RowLabel { get; init; } = string.Empty;
    public int SeatNumber { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? ExpiresAtUtc { get; init; }
    public bool HeldByCurrentUser { get; init; }
}

public sealed class SeatStateChangeDto
{
    public Guid SeatId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? ExpiresAtUtc { get; init; }
    public Guid? HoldGroupId { get; init; }
}

public sealed class SeatStateChangeBatchDto
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid ShowtimeId { get; init; }
    public long Version { get; init; }
    public DateTime CommittedAtUtc { get; init; }
    public IReadOnlyList<SeatStateChangeDto> Changes { get; init; } = [];
}
