using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Hubs;

[Authorize]
public sealed class SeatHub : Hub
{
    private readonly AppDbContext _dbContext;

    public SeatHub(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task JoinShowtime(Guid showtimeId)
    {
        if (!await _dbContext.Showtimes.AsNoTracking().AnyAsync(x => x.Id == showtimeId, Context.ConnectionAborted))
        {
            throw new HubException("Showtime was not found.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(showtimeId), Context.ConnectionAborted);
    }

    public Task LeaveShowtime(Guid showtimeId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(showtimeId), Context.ConnectionAborted);

    internal static string GroupName(Guid showtimeId) => $"showtime:{showtimeId:N}";
}
