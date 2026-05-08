using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class PaymentLog : BaseEntity
{
    public Guid PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;
    public string ResponseData { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
