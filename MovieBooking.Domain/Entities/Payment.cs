using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class Payment : BaseEntity
{
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string TransactionCode { get; set; } = string.Empty;
    public ICollection<PaymentLog> Logs { get; set; } = new List<PaymentLog>();
    public ICollection<PaymentOperation> Operations { get; set; } = new List<PaymentOperation>();
}
