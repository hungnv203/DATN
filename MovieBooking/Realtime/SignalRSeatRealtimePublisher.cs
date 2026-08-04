using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Hubs;

namespace MovieBooking.Realtime;

public sealed class SignalRSeatRealtimePublisher : ISeatRealtimePublisher
{
    private readonly IHubContext<SeatHub> _hubContext;
    private readonly ILogger<SignalRSeatRealtimePublisher> _logger;

    public SignalRSeatRealtimePublisher(
        IHubContext<SeatHub> hubContext,
        ILogger<SignalRSeatRealtimePublisher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task PublishAsync(SeatStateChangeBatchDto batch, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _hubContext.Clients.Group(SeatHub.GroupName(batch.ShowtimeId))
                .SendAsync("SeatStatusChanged", batch, cancellationToken);
            _logger.LogInformation(
                "Seat event published. EventId={EventId}, ShowtimeId={ShowtimeId}, Version={Version}, ChangeCount={ChangeCount}, DurationMs={DurationMs}",
                batch.EventId, batch.ShowtimeId, batch.Version, batch.Changes.Count, stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Seat event delivery failed after commit. EventId={EventId}, ShowtimeId={ShowtimeId}, Version={Version}",
                batch.EventId, batch.ShowtimeId, batch.Version);
        }
    }
}
