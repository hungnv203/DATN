namespace MovieBooking.Application.Common.DTOs;

public class BookingConcessionDto
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Guid ConcessionId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
