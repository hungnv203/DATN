namespace MovieBooking.Application.Common.DTOs;

public class BookingQuoteRequestDto
{
    public Guid ShowtimeId { get; set; }
    public List<Guid> SeatIds { get; set; } = new();
    public List<BookingConcessionDto> Concessions { get; set; } = new();
    public string? PromotionCode { get; set; }
    public int UsedPoints { get; set; }
}

public class BookingQuoteDto
{
    public decimal SeatTotal { get; set; }
    public decimal ConcessionTotal { get; set; }
    public decimal Subtotal { get; set; }
    public string? PromotionCode { get; set; }
    public decimal DiscountAmount { get; set; }
    public int UsedPoints { get; set; }
    public decimal PointDiscountAmount { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Message { get; set; }
}
