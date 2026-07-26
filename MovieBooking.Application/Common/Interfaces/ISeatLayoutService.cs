using MovieBooking.Application.Common.DTOs;

namespace MovieBooking.Application.Common.Interfaces;

public interface ISeatLayoutService
{
    Task<IReadOnlyList<SeatDto>> CreateBulkAsync(
        BulkSeatLayoutDto layout,
        CancellationToken cancellationToken = default);
}
