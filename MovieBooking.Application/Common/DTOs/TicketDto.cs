namespace MovieBooking.Application.Common.DTOs;

public class TicketDto
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Guid SeatId { get; set; }
    public decimal Price { get; set; }
    public string QrCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string MovieTitle { get; set; } = string.Empty;
    public string SeatLabel { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
}
