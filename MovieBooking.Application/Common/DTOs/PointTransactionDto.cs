namespace MovieBooking.Application.Common.DTOs;

public class PointTransactionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? BookingId { get; set; }
    public int Points { get; set; }
    public string Type { get; set; } = string.Empty;
    public int BalanceAfter { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
