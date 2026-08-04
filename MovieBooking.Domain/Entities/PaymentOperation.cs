using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public sealed class PaymentOperation : BaseEntity
{
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public Guid? PaymentId { get; set; }
    public Payment? Payment { get; set; }
    public Guid? ClientIdempotencyKey { get; set; }
    public string? ProviderEventKey { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
