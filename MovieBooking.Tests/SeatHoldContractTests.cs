using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Controllers;
using MovieBooking.Domain.Constants;
using MovieBooking.Domain.Entities;
using Xunit;

namespace MovieBooking.Tests;

public sealed class SeatHoldContractTests
{
    [Fact]
    public void DedicatedSeatHoldController_RequiresAuthentication()
    {
        var authorization = typeof(SeatHoldsController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorization);
    }

    [Fact]
    public void DedicatedSeatHoldController_ExposesLifecycleOperations()
    {
        var create = typeof(SeatHoldsController).GetMethod(nameof(SeatHoldsController.Create));
        var get = typeof(SeatHoldsController).GetMethod(nameof(SeatHoldsController.GetByGroupId));
        var replace = typeof(SeatHoldsController).GetMethod(nameof(SeatHoldsController.Replace));
        var release = typeof(SeatHoldsController).GetMethod(nameof(SeatHoldsController.Release));

        Assert.NotNull(create?.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(get?.GetCustomAttribute<HttpGetAttribute>());
        Assert.NotNull(replace?.GetCustomAttribute<HttpPutAttribute>());
        Assert.NotNull(release?.GetCustomAttribute<HttpDeleteAttribute>());
    }

    [Fact]
    public void SeatHoldLifecycle_UsesStableCanonicalStatuses()
    {
        Assert.Equal("Active", SeatHoldStatuses.Active);
        Assert.Equal("Released", SeatHoldStatuses.Released);
        Assert.Equal("Expired", SeatHoldStatuses.Expired);
        Assert.Equal("Completed", SeatHoldStatuses.Completed);
    }

    [Fact]
    public void BookingContract_CarriesOptionalSeatHoldGroup()
    {
        var holdGroupId = Guid.NewGuid();
        var dto = new BookingDto { SeatHoldGroupId = holdGroupId };
        var entity = new Booking { SeatHoldGroupId = holdGroupId };

        Assert.Equal(holdGroupId, dto.SeatHoldGroupId);
        Assert.Equal(holdGroupId, entity.SeatHoldGroupId);
    }
}
