namespace MovieBooking.Application.Common.DTOs;

public class BookingConcessionDto
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Guid ConcessionId { get; set; }
    public string ConcessionName { get; set; } = string.Empty;
    public string ConcessionImageUrl { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
