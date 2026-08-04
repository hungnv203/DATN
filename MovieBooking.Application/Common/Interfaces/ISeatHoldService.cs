using MovieBooking.Application.Common.DTOs;
namespace MovieBooking.Application.Common.Interfaces;

public interface ISeatHoldService
{
    Task<SeatHoldResultDto> CreateOrReplaceForShowtimeAsync(
        Guid userId,
        HoldSeatsRequestDto request,
        CancellationToken cancellationToken = default);

    Task<SeatHoldResultDto?> GetOwnedGroupAsync(
        Guid userId,
        Guid holdGroupId,
        CancellationToken cancellationToken = default);

    Task<SeatHoldResultDto> ReplaceAsync(
        Guid userId,
        Guid holdGroupId,
        HoldSeatsRequestDto request,
        CancellationToken cancellationToken = default);

    Task<SeatStateChangeBatchDto?> ReleaseAsync(
        Guid userId,
        Guid holdGroupId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SeatStateChangeBatchDto>> ExpireElapsedAsync(CancellationToken cancellationToken = default);
}
