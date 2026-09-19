using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Exceptions;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Constants;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;
using MovieBooking.Infrastructure.Mapping;
using MovieBooking.Infrastructure.Services;
using Npgsql;
using Xunit;

namespace MovieBooking.Tests;

[Trait("Category", "PostgreSqlIntegration")]
public sealed class SeatHoldPostgresIntegrationTests
{
    [PostgreSqlFact]
    public async Task Concurrent_users_cannot_hold_the_same_seat()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            var seeded = await SeedAsync(connectionString, seatCount: 1);
            var clock = new MutableTimeProvider(seeded.Now);
            await using var firstContext = CreateContext(connectionString);
            await using var secondContext = CreateContext(connectionString);
            var firstService = CreateService(firstContext, clock);
            var secondService = CreateService(secondContext, clock);
            var request = new HoldSeatsRequestDto
            {
                ShowtimeId = seeded.ShowtimeId,
                SeatIds = [seeded.SeatIds[0]]
            };

            var results = await Task.WhenAll(
                firstService.CreateOrReplaceForShowtimeAsync(seeded.FirstUserId, request),
                secondService.CreateOrReplaceForShowtimeAsync(seeded.SecondUserId, request));

            Assert.Single(results, result => result.Success);
            Assert.Single(results, result => result.ErrorCode == SeatHoldErrors.SeatNotAvailable);
            await using var verification = CreateContext(connectionString);
            Assert.Equal(1, await verification.SeatHolds.CountAsync(hold =>
                hold.ShowtimeId == seeded.ShowtimeId
                && hold.SeatId == seeded.SeatIds[0]
                && hold.Status == SeatHoldStatuses.Active));
        });
    }

    [PostgreSqlFact]
    public async Task Repeating_the_same_hold_is_a_no_op_and_does_not_extend_expiry()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            var seeded = await SeedAsync(connectionString, seatCount: 2);
            var clock = new MutableTimeProvider(seeded.Now);
            await using var context = CreateContext(connectionString);
            var service = CreateService(context, clock);
            var request = new HoldSeatsRequestDto
            {
                ShowtimeId = seeded.ShowtimeId,
                SeatIds = seeded.SeatIds.ToList()
            };
            var created = await service.CreateOrReplaceForShowtimeAsync(seeded.FirstUserId, request);
            var originalVersion = created.ChangeBatch!.Version;
            clock.Advance(TimeSpan.FromMinutes(1));

            var repeated = await service.CreateOrReplaceForShowtimeAsync(seeded.FirstUserId, request);

            Assert.True(repeated.Success);
            Assert.Equal(created.HoldGroupId, repeated.HoldGroupId);
            Assert.Equal(created.ExpiredAt, repeated.ExpiredAt);
            Assert.Null(repeated.ChangeBatch);
            await using var verification = CreateContext(connectionString);
            var version = await verification.ShowtimeSeatVersions
                .Where(item => item.ShowtimeId == seeded.ShowtimeId)
                .Select(item => item.Version)
                .SingleAsync();
            Assert.Equal(originalVersion, version);
        });
    }

    [PostgreSqlFact]
    public async Task Legacy_groups_are_consolidated_at_the_earliest_expiry()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            var seeded = await SeedAsync(connectionString, seatCount: 3);
            var earlyExpiry = seeded.Now.AddMinutes(3);
            var laterExpiry = seeded.Now.AddMinutes(5);
            await using (var setup = CreateContext(connectionString))
            {
                setup.SeatHolds.AddRange(
                    new SeatHold
                    {
                        HoldGroupId = Guid.NewGuid(),
                        ShowtimeId = seeded.ShowtimeId,
                        SeatId = seeded.SeatIds[0],
                        UserId = seeded.FirstUserId,
                        Status = SeatHoldStatuses.Active,
                        ExpiredAt = earlyExpiry
                    },
                    new SeatHold
                    {
                        HoldGroupId = Guid.NewGuid(),
                        ShowtimeId = seeded.ShowtimeId,
                        SeatId = seeded.SeatIds[1],
                        UserId = seeded.FirstUserId,
                        Status = SeatHoldStatuses.Active,
                        ExpiredAt = laterExpiry
                    });
                await setup.SaveChangesAsync();
            }

            var clock = new MutableTimeProvider(seeded.Now);
            await using var context = CreateContext(connectionString);
            var service = CreateService(context, clock);
            var result = await service.CreateOrReplaceForShowtimeAsync(
                seeded.FirstUserId,
                new HoldSeatsRequestDto
                {
                    ShowtimeId = seeded.ShowtimeId,
                    SeatIds = seeded.SeatIds.ToList()
                });

            Assert.True(result.Success);
            Assert.Equal(earlyExpiry, result.ExpiredAt);
            await using var verification = CreateContext(connectionString);
            var activeRows = await verification.SeatHolds
                .Where(hold => hold.ShowtimeId == seeded.ShowtimeId
                               && hold.UserId == seeded.FirstUserId
                               && hold.Status == SeatHoldStatuses.Active)
                .ToListAsync();
            Assert.Equal(3, activeRows.Count);
            Assert.Single(activeRows.Select(hold => hold.HoldGroupId).Distinct());
            Assert.All(activeRows, hold => Assert.Equal(earlyExpiry, hold.ExpiredAt));
        });
    }

    [PostgreSqlFact]
    public async Task Release_of_a_booking_linked_hold_returns_typed_conflict()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            var seeded = await SeedAsync(connectionString, seatCount: 1);
            var groupId = Guid.NewGuid();
            await using (var setup = CreateContext(connectionString))
            {
                var booking = new Booking
                {
                    UserId = seeded.FirstUserId,
                    ShowtimeId = seeded.ShowtimeId,
                    SeatHoldGroupId = groupId,
                    Status = BookingStatuses.Pending,
                    Channel = BookingChannels.CustomerOnline,
                    ExpiredAt = seeded.Now.AddMinutes(5)
                };
                setup.Bookings.Add(booking);
                setup.SeatHolds.Add(new SeatHold
                {
                    HoldGroupId = groupId,
                    ShowtimeId = seeded.ShowtimeId,
                    SeatId = seeded.SeatIds[0],
                    UserId = seeded.FirstUserId,
                    BookingId = booking.Id,
                    Status = SeatHoldStatuses.Active,
                    ExpiredAt = seeded.Now.AddMinutes(5)
                });
                await setup.SaveChangesAsync();
            }

            await using var context = CreateContext(connectionString);
            var service = CreateService(context, new MutableTimeProvider(seeded.Now));
            var result = await service.ReleaseAsync(seeded.FirstUserId, groupId);

            Assert.False(result.Success);
            Assert.Equal(SeatHoldErrors.HoldAlreadyBooked, result.ErrorCode);
            Assert.Null(result.ChangeBatch);
        });
    }

    [PostgreSqlFact]
    public async Task Concurrent_releases_finish_without_an_unhandled_database_error()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            var seeded = await SeedAsync(connectionString, seatCount: 1);
            var clock = new MutableTimeProvider(seeded.Now);
            Guid holdGroupId;
            await using (var createContext = CreateContext(connectionString))
            {
                var created = await CreateService(createContext, clock)
                    .CreateOrReplaceForShowtimeAsync(
                        seeded.FirstUserId,
                        new HoldSeatsRequestDto
                        {
                            ShowtimeId = seeded.ShowtimeId,
                            SeatIds = [seeded.SeatIds[0]]
                        });
                holdGroupId = created.HoldGroupId!.Value;
            }

            await using var firstContext = CreateContext(connectionString);
            await using var secondContext = CreateContext(connectionString);
            var results = await Task.WhenAll(
                CreateService(firstContext, clock).ReleaseAsync(seeded.FirstUserId, holdGroupId),
                CreateService(secondContext, clock).ReleaseAsync(seeded.FirstUserId, holdGroupId));

            Assert.Single(results, result => result.Success);
            Assert.Single(results, result => !result.Success
                && result.ErrorCode is SeatHoldErrors.HoldNotFound or SeatHoldErrors.SeatNotAvailable);
        });
    }

    [PostgreSqlFact]
    public async Task Concurrent_booking_and_replace_finish_in_a_serializable_business_state()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            var seeded = await SeedAsync(connectionString, seatCount: 2);
            var clock = new MutableTimeProvider(seeded.Now);
            SeatHoldResultDto created;
            await using (var createContext = CreateContext(connectionString))
            {
                created = await CreateService(createContext, clock)
                    .CreateOrReplaceForShowtimeAsync(
                        seeded.FirstUserId,
                        new HoldSeatsRequestDto
                        {
                            ShowtimeId = seeded.ShowtimeId,
                            SeatIds = [seeded.SeatIds[0]]
                        });
            }

            await using var bookingContext = CreateContext(connectionString);
            await using var replaceContext = CreateContext(connectionString);
            var bookingService = CreateBookingService(
                bookingContext, seeded.FirstUserId, clock);
            var replaceService = CreateService(replaceContext, clock);
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var bookingTask = Task.Run(async () =>
            {
                await gate.Task;
                try
                {
                    await bookingService.CreateAsync(new BookingDto
                    {
                        ShowtimeId = seeded.ShowtimeId,
                        SeatIds = [seeded.SeatIds[0]],
                        SeatHoldGroupId = created.HoldGroupId
                    });
                    return "success";
                }
                catch (SeatHoldConflictException)
                {
                    return "conflict";
                }
            });
            var replaceTask = Task.Run(async () =>
            {
                await gate.Task;
                return await replaceService.ReplaceAsync(
                    seeded.FirstUserId,
                    created.HoldGroupId!.Value,
                    new HoldSeatsRequestDto
                    {
                        ShowtimeId = seeded.ShowtimeId,
                        SeatIds = [seeded.SeatIds[1]]
                    });
            });
            gate.SetResult();

            var bookingOutcome = await bookingTask;
            var replaceOutcome = await replaceTask;
            Assert.True(bookingOutcome == "success" ^ replaceOutcome.Success);
            if (!replaceOutcome.Success)
            {
                Assert.Equal(SeatHoldErrors.HoldAlreadyBooked, replaceOutcome.ErrorCode);
            }

            await using var verification = CreateContext(connectionString);
            var booking = await verification.Bookings
                .Include(item => item.Tickets)
                .SingleOrDefaultAsync(item => item.SeatHoldGroupId == created.HoldGroupId);
            var activeHolds = await verification.SeatHolds
                .Where(hold => hold.HoldGroupId == created.HoldGroupId
                               && hold.Status == SeatHoldStatuses.Active)
                .OrderBy(hold => hold.SeatId)
                .ToListAsync();
            if (booking != null)
            {
                Assert.Equal(
                    booking.Tickets.Select(ticket => ticket.SeatId).OrderBy(id => id),
                    activeHolds.Where(hold => hold.BookingId == booking.Id)
                        .Select(hold => hold.SeatId)
                        .OrderBy(id => id));
            }
            else
            {
                Assert.Single(activeHolds, hold => hold.SeatId == seeded.SeatIds[1]);
            }
        });
    }

    [PostgreSqlFact]
    public async Task Concurrent_booking_and_release_finish_in_a_serializable_business_state()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            var seeded = await SeedAsync(connectionString, seatCount: 1);
            var clock = new MutableTimeProvider(seeded.Now);
            SeatHoldResultDto created;
            await using (var createContext = CreateContext(connectionString))
            {
                created = await CreateService(createContext, clock)
                    .CreateOrReplaceForShowtimeAsync(
                        seeded.FirstUserId,
                        new HoldSeatsRequestDto
                        {
                            ShowtimeId = seeded.ShowtimeId,
                            SeatIds = [seeded.SeatIds[0]]
                        });
            }

            await using var bookingContext = CreateContext(connectionString);
            await using var releaseContext = CreateContext(connectionString);
            var bookingService = CreateBookingService(bookingContext, seeded.FirstUserId, clock);
            var releaseService = CreateService(releaseContext, clock);
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var bookingTask = Task.Run(async () =>
            {
                await gate.Task;
                try
                {
                    await bookingService.CreateAsync(new BookingDto
                    {
                        ShowtimeId = seeded.ShowtimeId,
                        SeatIds = [seeded.SeatIds[0]],
                        SeatHoldGroupId = created.HoldGroupId
                    });
                    return "success";
                }
                catch (SeatHoldConflictException)
                {
                    return "conflict";
                }
            });
            var releaseTask = Task.Run(async () =>
            {
                await gate.Task;
                return await releaseService.ReleaseAsync(
                    seeded.FirstUserId,
                    created.HoldGroupId!.Value);
            });
            gate.SetResult();

            var bookingOutcome = await bookingTask;
            var releaseOutcome = await releaseTask;
            Assert.True(bookingOutcome == "success" ^ releaseOutcome.Success);
            if (!releaseOutcome.Success && bookingOutcome == "success")
            {
                Assert.Equal(SeatHoldErrors.HoldAlreadyBooked, releaseOutcome.ErrorCode);
            }

            await using var verification = CreateContext(connectionString);
            var booking = await verification.Bookings
                .Include(item => item.Tickets)
                .SingleOrDefaultAsync(item => item.SeatHoldGroupId == created.HoldGroupId);
            var hold = await verification.SeatHolds
                .SingleAsync(item => item.HoldGroupId == created.HoldGroupId);
            if (booking != null)
            {
                Assert.Equal(SeatHoldStatuses.Active, hold.Status);
                Assert.Equal(booking.Id, hold.BookingId);
                Assert.Single(booking.Tickets, ticket => ticket.SeatId == hold.SeatId);
            }
            else
            {
                Assert.Equal(SeatHoldStatuses.Released, hold.Status);
                Assert.Null(hold.BookingId);
            }
        });
    }

    [PostgreSqlFact]
    public async Task Database_unique_index_rejects_two_active_holds_for_the_same_showtime_seat()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            var seeded = await SeedAsync(connectionString, seatCount: 1);
            var expiry = seeded.Now.AddMinutes(5);
            await using var firstContext = CreateContext(connectionString);
            await using var secondContext = CreateContext(connectionString);
            firstContext.SeatHolds.Add(new SeatHold
            {
                HoldGroupId = Guid.NewGuid(),
                ShowtimeId = seeded.ShowtimeId,
                SeatId = seeded.SeatIds[0],
                UserId = seeded.FirstUserId,
                Status = SeatHoldStatuses.Active,
                ExpiredAt = expiry
            });
            secondContext.SeatHolds.Add(new SeatHold
            {
                HoldGroupId = Guid.NewGuid(),
                ShowtimeId = seeded.ShowtimeId,
                SeatId = seeded.SeatIds[0],
                UserId = seeded.SecondUserId,
                Status = SeatHoldStatuses.Active,
                ExpiredAt = expiry
            });

            await firstContext.SaveChangesAsync();
            var exception = await Record.ExceptionAsync(() => secondContext.SaveChangesAsync());

            var updateException = Assert.IsType<DbUpdateException>(exception);
            var postgresException = Assert.IsType<PostgresException>(updateException.InnerException);
            Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
            await using var verification = CreateContext(connectionString);
            Assert.Equal(1, await verification.SeatHolds.CountAsync(hold =>
                hold.ShowtimeId == seeded.ShowtimeId
                && hold.SeatId == seeded.SeatIds[0]
                && hold.Status == SeatHoldStatuses.Active));
        });
    }

    [PostgreSqlFact]
    public async Task Failed_replace_rolls_back_without_partial_hold_or_version_changes()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            var seeded = await SeedAsync(connectionString, seatCount: 2);
            var clock = new MutableTimeProvider(seeded.Now);
            SeatHoldResultDto created;
            await using (var createContext = CreateContext(connectionString))
            {
                created = await CreateService(createContext, clock)
                    .CreateOrReplaceForShowtimeAsync(
                        seeded.FirstUserId,
                        new HoldSeatsRequestDto
                        {
                            ShowtimeId = seeded.ShowtimeId,
                            SeatIds = [seeded.SeatIds[0]]
                        });
            }
            var originalVersion = created.ChangeBatch!.Version;

            await using (var replaceContext = CreateContext(connectionString))
            {
                var result = await CreateService(replaceContext, clock).ReplaceAsync(
                    seeded.FirstUserId,
                    created.HoldGroupId!.Value,
                    new HoldSeatsRequestDto
                    {
                        ShowtimeId = seeded.ShowtimeId,
                        SeatIds = [seeded.SeatIds[1], Guid.NewGuid()]
                    });

                Assert.False(result.Success);
                Assert.Equal(SeatHoldErrors.InvalidSeatSelection, result.ErrorCode);
                Assert.Null(result.ChangeBatch);
            }

            await using var verification = CreateContext(connectionString);
            var activeRows = await verification.SeatHolds
                .Where(hold => hold.HoldGroupId == created.HoldGroupId
                               && hold.Status == SeatHoldStatuses.Active)
                .ToListAsync();
            Assert.Single(activeRows, hold => hold.SeatId == seeded.SeatIds[0]);
            Assert.DoesNotContain(activeRows, hold => hold.SeatId == seeded.SeatIds[1]);
            var version = await verification.ShowtimeSeatVersions
                .Where(item => item.ShowtimeId == seeded.ShowtimeId)
                .Select(item => item.Version)
                .SingleAsync();
            Assert.Equal(originalVersion, version);
        });
    }

    private static SeatHoldService CreateService(AppDbContext context, TimeProvider clock) =>
        new(context, NullLogger<SeatHoldService>.Instance, clock);

    private static BookingService CreateBookingService(
        AppDbContext context,
        Guid userId,
        TimeProvider clock)
    {
        var mapperConfiguration = new MapperConfiguration(
            configuration => configuration.AddProfile<EntityDtoProfile>(),
            NullLoggerFactory.Instance);
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                    "Test"))
            }
        };
        return new BookingService(
            context,
            mapperConfiguration.CreateMapper(),
            accessor,
            new FixedPricingService(),
            clock,
            NullLogger<BookingService>.Instance);
    }

    private static AppDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<SeededData> SeedAsync(string connectionString, int seatCount)
    {
        var now = new DateTime(2026, 9, 19, 2, 0, 0, DateTimeKind.Utc);
        await using var context = CreateContext(connectionString);
        var cinema = new Cinema { Name = "Integration Cinema", Address = "Test", City = "Test" };
        var room = new Room { Cinema = cinema, Name = "Room 1", TotalSeats = seatCount, Type = "2D" };
        var seats = Enumerable.Range(1, seatCount)
            .Select(number => new Seat { Room = room, RowLabel = "A", SeatNumber = number, Type = "Standard" })
            .ToArray();
        var movie = new Movie
        {
            Title = "Integration Movie",
            Description = "Test",
            Duration = 120,
            ReleaseDate = now.AddDays(-1),
            Language = "vi",
            Rating = "P",
            Status = "Active"
        };
        var showtime = new Showtime
        {
            Movie = movie,
            Room = room,
            StartTime = now.AddHours(2),
            EndTime = now.AddHours(4),
            BasePrice = 100_000,
            Status = "Active"
        };
        var firstUser = new User { FullName = "First", Email = $"first-{Guid.NewGuid():N}@test.local" };
        var secondUser = new User { FullName = "Second", Email = $"second-{Guid.NewGuid():N}@test.local" };
        context.AddRange(cinema, room, movie, showtime, firstUser, secondUser);
        context.Seats.AddRange(seats);
        await context.SaveChangesAsync();
        return new SeededData(
            now,
            showtime.Id,
            firstUser.Id,
            secondUser.Id,
            seats.Select(seat => seat.Id).ToArray());
    }

    private static async Task WithDatabaseAsync(Func<string, Task> test)
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("MOVIEBOOKING_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            throw new InvalidOperationException(
                "Set MOVIEBOOKING_TEST_POSTGRES to an isolated PostgreSQL database or maintenance database.");
        }

        var schema = $"seat_hold_test_{Guid.NewGuid():N}";
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString) { SearchPath = schema };
        var adminBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString) { SearchPath = string.Empty };
        await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin))
        {
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            await using var context = CreateContext(builder.ConnectionString);
            await context.Database.EnsureCreatedAsync();
            await test(builder.ConnectionString);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed record SeededData(
        DateTime Now,
        Guid ShowtimeId,
        Guid FirstUserId,
        Guid SecondUserId,
        Guid[] SeatIds);

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public MutableTimeProvider(DateTime utcNow) => _utcNow = new DateTimeOffset(utcNow);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }

    private sealed class FixedPricingService : IPricingService
    {
        public Task<BookingQuoteDto> QuoteAsync(
            BookingQuoteRequestDto request,
            Guid? userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new BookingQuoteDto
            {
                SeatTotal = request.SeatIds.Count * 100_000,
                Subtotal = request.SeatIds.Count * 100_000,
                TotalPrice = request.SeatIds.Count * 100_000
            });
    }
}

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("MOVIEBOOKING_TEST_POSTGRES")))
        {
            Skip = "Set MOVIEBOOKING_TEST_POSTGRES to an isolated PostgreSQL database or maintenance database.";
        }
    }
}
