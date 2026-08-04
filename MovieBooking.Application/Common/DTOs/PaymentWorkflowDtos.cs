namespace MovieBooking.Application.Common.DTOs;

public sealed class CreatePosBookingRequestDto
{
    public Guid ShowtimeId { get; init; }
    public IReadOnlyList<Guid> SeatIds { get; init; } = [];
    public Guid SeatHoldGroupId { get; init; }
}

public sealed class PosPaymentConfirmationRequestDto
{
    public string Method { get; init; } = string.Empty;
}

public sealed class PosCancellationRequestDto
{
    public string ReasonCode { get; init; } = string.Empty;
}

public sealed class PaymentTransitionResultDto
{
    public bool Success { get; init; }
    public bool IsReplay { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string PaymentState { get; init; } = string.Empty;
    public BookingDto? Booking { get; init; }
    public SeatStateChangeBatchDto? ChangeBatch { get; init; }
}

public sealed class ProviderPaymentCommandDto
{
    public Guid PaymentId { get; init; }
    public string ProviderEventKey { get; init; } = string.Empty;
    public string ProviderTransactionCode { get; init; } = string.Empty;
    public bool Succeeded { get; init; }
    public bool ConfirmedFailure { get; init; }
}
