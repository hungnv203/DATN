using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Controllers;
using MovieBooking.Domain.Constants;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;
using MovieBooking.Infrastructure.Services;
using Npgsql;
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

    [Fact]
    public void SeatHoldErrors_DefinesStableErrorCodes()
    {
        Assert.Equal("HOLD_SEAT_LIMIT_EXCEEDED", SeatHoldErrors.HoldSeatLimitExceeded);
        Assert.Equal("SHOWTIME_NOT_BOOKABLE", SeatHoldErrors.ShowtimeNotBookable);
        Assert.Equal("HOLD_ALREADY_BOOKED", SeatHoldErrors.HoldAlreadyBooked);
        Assert.Equal("BOOKING_ALREADY_PENDING", SeatHoldErrors.BookingAlreadyPending);
        Assert.Equal("SEAT_NOT_AVAILABLE", SeatHoldErrors.SeatNotAvailable);
        Assert.Equal("SHOWTIME_NOT_FOUND", SeatHoldErrors.ShowtimeNotFound);
        Assert.Equal("HOLD_NOT_FOUND", SeatHoldErrors.HoldNotFound);
        Assert.Equal("INVALID_REQUEST", SeatHoldErrors.InvalidRequest);
        Assert.Equal("INVALID_SEAT_SELECTION", SeatHoldErrors.InvalidSeatSelection);
    }

    [Fact]
    public void SeatHoldErrors_MaxSeatsPerHold_IsEight()
    {
        Assert.Equal(8, SeatHoldErrors.MaxSeatsPerHold);
    }

    [Fact]
    public void SeatHoldErrors_HoldTtlMinutes_IsFive()
    {
        Assert.Equal(5, SeatHoldErrors.HoldTtlMinutes);
    }

    [Fact]
    public void SeatHoldErrors_RateLimitPerMinute_IsTen()
    {
        Assert.Equal(10, SeatHoldErrors.RateLimitPerMinute);
    }

    [Fact]
    public void SeatHoldController_Create_IsRateLimited()
    {
        var create = typeof(SeatHoldsController).GetMethod(nameof(SeatHoldsController.Create));
        var rateLimit = create?.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(rateLimit);
        Assert.Equal("SeatHoldMutation", rateLimit.PolicyName);
    }

    [Fact]
    public void SeatHoldController_Replace_IsRateLimited()
    {
        var replace = typeof(SeatHoldsController).GetMethod(nameof(SeatHoldsController.Replace));
        var rateLimit = replace?.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(rateLimit);
        Assert.Equal("SeatHoldMutation", rateLimit.PolicyName);
    }

    [Fact]
    public void SeatHoldController_Release_IsNotRateLimited()
    {
        var release = typeof(SeatHoldsController).GetMethod(nameof(SeatHoldsController.Release));
        var rateLimit = release?.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.Null(rateLimit);
    }

    [Fact]
    public void SeatHoldController_Get_IsNotRateLimited()
    {
        var get = typeof(SeatHoldsController).GetMethod(nameof(SeatHoldsController.GetByGroupId));
        var rateLimit = get?.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.Null(rateLimit);
    }

    [Fact]
    public void LegacyHoldSeats_IsRateLimited()
    {
        var holdSeats = typeof(BookingsController).GetMethod(nameof(BookingsController.HoldSeats));
        var rateLimit = holdSeats?.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(rateLimit);
        Assert.Equal("SeatHoldMutation", rateLimit.PolicyName);
    }

    [Fact]
    public void SeatHold_HasBookingIdField()
    {
        var hold = new SeatHold();
        Assert.Null(hold.BookingId);

        var bookingId = Guid.NewGuid();
        hold.BookingId = bookingId;
        Assert.Equal(bookingId, hold.BookingId);
    }

    [Fact]
    public void SeatHoldResultDto_CarriesErrorCode()
    {
        var result = new SeatHoldResultDto
        {
            Success = false,
            ErrorCode = SeatHoldErrors.HoldAlreadyBooked,
            Message = "Test"
        };

        Assert.Equal(SeatHoldErrors.HoldAlreadyBooked, result.ErrorCode);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Release_AfterBooking_ReturnsTypedConflictWithoutPublishing()
    {
        var userId = Guid.NewGuid();
        var service = new StubSeatHoldService(new SeatHoldResultDto
        {
            Success = false,
            ErrorCode = SeatHoldErrors.HoldAlreadyBooked,
            Message = "Already linked to a booking."
        });
        var publisher = new RecordingPublisher();
        var controller = new SeatHoldsController(service, publisher)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                        "Test"))
                }
            }
        };

        var response = await controller.Release(Guid.NewGuid(), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(response);
        var payload = Assert.IsType<SeatHoldResultDto>(conflict.Value);
        Assert.Equal(SeatHoldErrors.HoldAlreadyBooked, payload.ErrorCode);
        Assert.Equal(0, publisher.PublishCount);
    }

    [Fact]
    public async Task Retry_exhaustion_returns_a_stable_conflict_after_three_attempts()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"seat-hold-retry-{Guid.NewGuid():N}")
            .Options;
        await using var context = new AppDbContext(options);
        var service = new SeatHoldService(
            context,
            NullLogger<SeatHoldService>.Instance,
            TimeProvider.System);
        var attempts = 0;
        Func<Task<SeatHoldResultDto>> operation = () =>
        {
            attempts++;
            throw new PostgresException(
                "serialization failure",
                "ERROR",
                "ERROR",
                PostgresErrorCodes.SerializationFailure);
        };
        var retryMethod = typeof(SeatHoldService).GetMethod(
            "ExecuteWithRetryAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(retryMethod);

        var pendingResult = Assert.IsAssignableFrom<Task<SeatHoldResultDto>>(retryMethod.Invoke(
            service,
            [operation, Guid.NewGuid(), 1, CancellationToken.None]));
        var result = await pendingResult;

        Assert.Equal(3, attempts);
        Assert.False(result.Success);
        Assert.Equal(SeatHoldErrors.SeatNotAvailable, result.ErrorCode);
        Assert.Null(result.ChangeBatch);
    }

    private sealed class StubSeatHoldService(SeatHoldResultDto releaseResult) : ISeatHoldService
    {
        public Task<SeatHoldResultDto> CreateOrReplaceForShowtimeAsync(
            Guid userId, HoldSeatsRequestDto request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SeatHoldResultDto?> GetOwnedGroupAsync(
            Guid userId, Guid holdGroupId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SeatHoldResultDto> ReplaceAsync(
            Guid userId, Guid holdGroupId, HoldSeatsRequestDto request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SeatHoldResultDto> ReleaseAsync(
            Guid userId, Guid holdGroupId, CancellationToken cancellationToken = default) =>
            Task.FromResult(releaseResult);

        public Task<IReadOnlyList<SeatStateChangeBatchDto>> ExpireElapsedAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingPublisher : ISeatRealtimePublisher
    {
        public int PublishCount { get; private set; }

        public Task PublishAsync(
            SeatStateChangeBatchDto batch, CancellationToken cancellationToken = default)
        {
            PublishCount++;
            return Task.CompletedTask;
        }
    }
}
