using MovieBooking.Application.Common.DTOs;

namespace MovieBooking.Application.Common.Interfaces;

public interface ISeatRealtimePublisher
{
    Task PublishAsync(SeatStateChangeBatchDto batch, CancellationToken cancellationToken = default);
}
