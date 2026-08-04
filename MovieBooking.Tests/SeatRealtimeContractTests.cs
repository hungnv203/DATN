using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Hubs;
using Xunit;

namespace MovieBooking.Tests;

public sealed class SeatRealtimeContractTests
{
    [Fact]
    public void SeatHub_RequiresAuthentication()
    {
        Assert.NotNull(typeof(SeatHub).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void RealtimeEvent_UsesPerSeatOpaqueHoldGroupWithoutUserIdentity()
    {
        var eventProperties = typeof(SeatStateChangeBatchDto).GetProperties().Select(x => x.Name).ToArray();
        var changeProperties = typeof(SeatStateChangeDto).GetProperties().Select(x => x.Name).ToArray();

        Assert.DoesNotContain("UserId", eventProperties);
        Assert.DoesNotContain("HoldGroupId", eventProperties);
        Assert.DoesNotContain("UserId", changeProperties);
        Assert.Contains("HoldGroupId", changeProperties);
    }

    [Fact]
    public void VersionedSnapshot_DoesNotExposeHolderIdentity()
    {
        var properties = typeof(RealtimeShowtimeSeatDto).GetProperties().Select(x => x.Name).ToArray();

        Assert.Contains("HeldByCurrentUser", properties);
        Assert.DoesNotContain("HeldByUserId", properties);
        Assert.DoesNotContain("HoldGroupId", properties);
    }
}
