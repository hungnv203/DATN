namespace MovieBooking.Application.Common.DTOs;

public class PaymentLogDto
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public string ResponseData { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
