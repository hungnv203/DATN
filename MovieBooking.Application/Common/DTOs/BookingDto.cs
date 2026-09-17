namespace MovieBooking.Application.Common.DTOs;

public class BookingDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ShowtimeId { get; set; }
    public Guid? SeatHoldGroupId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal PointDiscountAmount { get; set; }
    public int UsedPoints { get; set; }
    public string? PromotionCode { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public List<Guid> SeatIds { get; set; } = new();
    public List<BookingConcessionDto> Concessions { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public string CinemaName { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public DateTime ShowtimeStartTime { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public List<string> SeatLabels { get; set; } = new();
}
