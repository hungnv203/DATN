namespace MovieBooking.Application.Common.DTOs;

public class BulkSeatLayoutDto
{
    public Guid RoomId { get; set; }
    public List<BulkSeatItemDto> Seats { get; set; } = [];
}

public class BulkSeatItemDto
{
    public string RowLabel { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public string Type { get; set; } = string.Empty;
}
