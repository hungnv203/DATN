using MovieBooking.Application.Common.DTOs;

namespace MovieBooking.Application.Common.Interfaces;

public interface IPaymentWorkflowService
{
    Task<PaymentTransitionResultDto> ConfirmPosCashAsync(
        Guid actorUserId,
        Guid bookingId,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<PaymentTransitionResultDto> CancelPosAsync(
        Guid actorUserId,
        Guid bookingId,
        Guid idempotencyKey,
        string reasonCode,
        CancellationToken cancellationToken = default);

    Task<PaymentTransitionResultDto> ProcessProviderNotificationAsync(
        ProviderPaymentCommandDto command,
        CancellationToken cancellationToken = default);
}
