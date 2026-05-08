namespace MovieBooking.Application.Common.DTOs;

public class PointTransactionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int Points { get; set; }
    public string Type { get; set; } = string.Empty;
}
