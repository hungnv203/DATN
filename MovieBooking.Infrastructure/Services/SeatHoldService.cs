using System.Data;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Constants;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;
using Npgsql;

namespace MovieBooking.Infrastructure.Services;

internal sealed class SeatHoldService : ISeatHoldService
{
    private const int MaxTransactionAttempts = 3;
    private static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _dbContext;
    private readonly ILogger<SeatHoldService> _logger;
    private readonly TimeProvider _timeProvider;

    public SeatHoldService(
        AppDbContext dbContext,
        ILogger<SeatHoldService> logger,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public Task<SeatHoldResultDto> CreateOrReplaceForShowtimeAsync(
        Guid userId,
        HoldSeatsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithRetryAsync(
            () => UpsertAsync(userId, null, request, cancellationToken),
            request.ShowtimeId,
            request.SeatIds?.Count ?? 0,
            cancellationToken);
    }

    public async Task<SeatHoldResultDto?> GetOwnedGroupAsync(
        Guid userId,
        Guid holdGroupId,
        CancellationToken cancellationToken = default)
    {
        var now = GetUtcNow();
        var rows = await _dbContext.SeatHolds
            .AsNoTracking()
            .Where(hold => hold.HoldGroupId == holdGroupId && hold.UserId == userId)
            .OrderBy(hold => hold.SeatId)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return null;
        }

        var activeRows = rows
            .Where(hold => hold.Status == SeatHoldStatuses.Active && hold.ExpiredAt > now)
            .ToList();
        var representative = activeRows.FirstOrDefault() ?? rows[0];

        return BuildResult(
            activeRows.Count > 0,
            activeRows.Count > 0 ? "Seat hold is active." : "Seat hold is no longer active.",
            representative.HoldGroupId,
            representative.ShowtimeId,
            activeRows.Select(hold => hold.SeatId).ToArray(),
            activeRows.Count > 0
                ? SeatHoldStatuses.Active
                : representative.Status == SeatHoldStatuses.Active && representative.ExpiredAt <= now
                    ? SeatHoldStatuses.Expired
                    : representative.Status,
            representative.ExpiredAt,
            now);
    }

    public Task<SeatHoldResultDto> ReplaceAsync(
        Guid userId,
        Guid holdGroupId,
        HoldSeatsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithRetryAsync(
            () => UpsertAsync(userId, holdGroupId, request, cancellationToken),
            request.ShowtimeId,
            request.SeatIds?.Count ?? 0,
            cancellationToken);
    }

    public async Task<SeatStateChangeBatchDto?> ReleaseAsync(
        Guid userId,
        Guid holdGroupId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var now = GetUtcNow();
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var rows = await _dbContext.SeatHolds
            .Where(hold => hold.HoldGroupId == holdGroupId
                           && hold.UserId == userId
                           && hold.Status == SeatHoldStatuses.Active
                           && hold.ExpiredAt > now)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return null;
        }

        foreach (var row in rows)
        {
            row.Status = SeatHoldStatuses.Released;
            row.ReleasedAt = now;
            row.MarkUpdated(new DateTimeOffset(now, TimeSpan.Zero));
        }

        var version = await IncrementVersionAsync(rows[0].ShowtimeId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        LogOperation("Release", holdGroupId, rows[0].ShowtimeId, rows.Count, "Released", stopwatch.Elapsed);
        return CreateBatch(rows[0].ShowtimeId, version, now, rows.Select(row => new SeatStateChangeDto
        {
            SeatId = row.SeatId,
            Status = "Available",
            HoldGroupId = row.HoldGroupId
        }).ToArray());
    }

    public async Task<IReadOnlyList<SeatStateChangeBatchDto>> ExpireElapsedAsync(CancellationToken cancellationToken = default)
    {
        var now = GetUtcNow();
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var rows = await _dbContext.SeatHolds
            .Where(hold => hold.Status == SeatHoldStatuses.Active && hold.ExpiredAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.Status = SeatHoldStatuses.Expired;
            row.MarkUpdated(new DateTimeOffset(now, TimeSpan.Zero));
        }

        if (rows.Count == 0)
        {
            return [];
        }

        var batches = new List<SeatStateChangeBatchDto>();
        foreach (var group in rows.GroupBy(row => row.ShowtimeId))
        {
            var version = await IncrementVersionAsync(group.Key, cancellationToken);
            batches.Add(CreateBatch(group.Key, version, now, group.Select(row => new SeatStateChangeDto
            {
                SeatId = row.SeatId,
                Status = "Available",
                HoldGroupId = row.HoldGroupId
            }).ToArray()));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return batches;
    }

    private async Task<SeatHoldResultDto> UpsertAsync(
        Guid userId,
        Guid? requestedGroupId,
        HoldSeatsRequestDto request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateRequest(request);
        if (validation != null)
        {
            return validation;
        }

        var now = GetUtcNow();
        var requestedSeatIds = request.SeatIds.OrderBy(id => id).ToArray();
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var showtime = await _dbContext.Showtimes
            .AsNoTracking()
            .Where(item => item.Id == request.ShowtimeId)
            .Select(item => new { item.Id, item.RoomId })
            .SingleOrDefaultAsync(cancellationToken);
        if (showtime == null)
        {
            return Failure("SHOWTIME_NOT_FOUND", "Showtime was not found.", now);
        }

        var lockedSeats = await _dbContext.Seats
            .FromSqlRaw(
                "SELECT * FROM \"Seats\" WHERE \"Id\" = ANY({0}) AND \"RoomId\" = {1} ORDER BY \"Id\" FOR UPDATE",
                requestedSeatIds,
                showtime.RoomId)
            .ToListAsync(cancellationToken);
        if (lockedSeats.Count != requestedSeatIds.Length)
        {
            return Failure("INVALID_SEAT_SELECTION", "One or more seats do not belong to the showtime room.", now);
        }

        var elapsedRows = await _dbContext.SeatHolds
            .Where(hold => hold.ShowtimeId == request.ShowtimeId
                           && requestedSeatIds.Contains(hold.SeatId)
                           && hold.Status == SeatHoldStatuses.Active
                           && hold.ExpiredAt <= now)
            .ToListAsync(cancellationToken);
        foreach (var elapsedRow in elapsedRows)
        {
            elapsedRow.Status = SeatHoldStatuses.Expired;
            elapsedRow.MarkUpdated(new DateTimeOffset(now, TimeSpan.Zero));
        }

        List<SeatHold> ownedRows;
        Guid holdGroupId;
        if (requestedGroupId.HasValue)
        {
            ownedRows = await _dbContext.SeatHolds
                .Where(hold => hold.HoldGroupId == requestedGroupId.Value
                               && hold.UserId == userId
                               && hold.Status == SeatHoldStatuses.Active
                               && hold.ExpiredAt > now)
                .ToListAsync(cancellationToken);
            if (ownedRows.Count == 0 || ownedRows.Any(hold => hold.ShowtimeId != request.ShowtimeId))
            {
                return Failure("HOLD_NOT_FOUND", "Seat hold was not found.", now);
            }

            holdGroupId = requestedGroupId.Value;
        }
        else
        {
            ownedRows = await _dbContext.SeatHolds
                .Where(hold => hold.ShowtimeId == request.ShowtimeId
                               && hold.UserId == userId
                               && hold.Status == SeatHoldStatuses.Active
                               && hold.ExpiredAt > now
                               && hold.BookingId == null)
                .ToListAsync(cancellationToken);
            holdGroupId = ownedRows.Select(hold => hold.HoldGroupId).Distinct().Count() == 1
                ? ownedRows[0].HoldGroupId
                : Guid.NewGuid();
        }

        var paidConflict = await _dbContext.Tickets
            .AsNoTracking()
            .AnyAsync(ticket => requestedSeatIds.Contains(ticket.SeatId)
                                && ticket.Booking.ShowtimeId == request.ShowtimeId
                                && ticket.Booking.Status == "Paid", cancellationToken);
        var pendingConflict = await _dbContext.Tickets
            .AsNoTracking()
            .AnyAsync(ticket => requestedSeatIds.Contains(ticket.SeatId)
                                && ticket.Booking.ShowtimeId == request.ShowtimeId
                                && ticket.Booking.Status == "Pending"
                                && ticket.Booking.ExpiredAt > now, cancellationToken);
        var ownedRowIds = ownedRows.Select(hold => hold.Id).ToArray();
        var heldConflict = await _dbContext.SeatHolds
            .AsNoTracking()
            .AnyAsync(hold => hold.ShowtimeId == request.ShowtimeId
                              && requestedSeatIds.Contains(hold.SeatId)
                              && hold.Status == SeatHoldStatuses.Active
                              && hold.ExpiredAt > now
                              && !ownedRowIds.Contains(hold.Id), cancellationToken);

        if (paidConflict || pendingConflict || heldConflict)
        {
            return Failure("SEAT_NOT_AVAILABLE", "One or more selected seats are no longer available.", now);
        }

        var expiry = now.Add(HoldDuration);
        foreach (var row in ownedRows)
        {
            if (requestedSeatIds.Contains(row.SeatId))
            {
                row.HoldGroupId = holdGroupId;
                row.ExpiredAt = expiry;
                row.MarkUpdated(new DateTimeOffset(now, TimeSpan.Zero));
            }
            else
            {
                row.Status = SeatHoldStatuses.Released;
                row.ReleasedAt = now;
                row.MarkUpdated(new DateTimeOffset(now, TimeSpan.Zero));
            }
        }

        var retainedSeatIds = ownedRows
            .Where(row => row.Status == SeatHoldStatuses.Active && requestedSeatIds.Contains(row.SeatId))
            .Select(row => row.SeatId)
            .ToHashSet();
        var newRows = requestedSeatIds
            .Where(seatId => !retainedSeatIds.Contains(seatId))
            .Select(seatId => new SeatHold
            {
                HoldGroupId = holdGroupId,
                ShowtimeId = request.ShowtimeId,
                SeatId = seatId,
                UserId = userId,
                Status = SeatHoldStatuses.Active,
                ExpiredAt = expiry
            })
            .ToList();
        await _dbContext.SeatHolds.AddRangeAsync(newRows, cancellationToken);
        var releasedRows = ownedRows
            .Where(row => row.Status == SeatHoldStatuses.Released)
            .ToArray();
        var version = await IncrementVersionAsync(request.ShowtimeId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var result = BuildResult(
            true,
            "Seats held successfully.",
            holdGroupId,
            request.ShowtimeId,
            requestedSeatIds,
            SeatHoldStatuses.Active,
            expiry,
            now);
        result.ChangeBatch = CreateBatch(
            request.ShowtimeId,
            version,
            now,
            releasedRows.Select(row => new SeatStateChangeDto
            {
                SeatId = row.SeatId,
                Status = "Available",
                HoldGroupId = row.HoldGroupId
            }).Concat(requestedSeatIds.Select(seatId => new SeatStateChangeDto
            {
                SeatId = seatId,
                Status = "Held",
                ExpiresAtUtc = expiry,
                HoldGroupId = holdGroupId
            })).ToArray());
        return result;
    }

    private async Task<long> IncrementVersionAsync(Guid showtimeId, CancellationToken cancellationToken)
        => await ShowtimeSeatVersionStore.IncrementAsync(_dbContext, showtimeId, cancellationToken);

    private static SeatStateChangeBatchDto CreateBatch(
        Guid showtimeId,
        long version,
        DateTime committedAtUtc,
        IReadOnlyList<SeatStateChangeDto> changes) => new()
    {
        ShowtimeId = showtimeId,
        Version = version,
        CommittedAtUtc = committedAtUtc,
        Changes = changes
    };

    private async Task<SeatHoldResultDto> ExecuteWithRetryAsync(
        Func<Task<SeatHoldResultDto>> operation,
        Guid showtimeId,
        int seatCount,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        for (var attempt = 1; attempt <= MaxTransactionAttempts; attempt++)
        {
            try
            {
                var result = await operation();
                LogOperation("CreateOrReplace", result.HoldGroupId, showtimeId, seatCount,
                    result.Success ? "Success" : result.ErrorCode ?? "Failed", stopwatch.Elapsed);
                return result;
            }
            catch (Exception exception) when (IsRetryable(exception) && attempt < MaxTransactionAttempts)
            {
                _dbContext.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
            catch (Exception exception) when (IsRetryable(exception))
            {
                _dbContext.ChangeTracker.Clear();
                LogOperation("CreateOrReplace", null, showtimeId, seatCount, "ConcurrencyConflict", stopwatch.Elapsed);
                return Failure(
                    "SEAT_NOT_AVAILABLE",
                    "One or more selected seats are no longer available.",
                    GetUtcNow());
            }
        }

        throw new InvalidOperationException("Seat-hold retry loop ended unexpectedly.");
    }

    private SeatHoldResultDto? ValidateRequest(HoldSeatsRequestDto request)
    {
        var now = GetUtcNow();
        if (request.ShowtimeId == Guid.Empty || request.SeatIds == null || request.SeatIds.Count == 0)
        {
            return Failure("INVALID_REQUEST", "Showtime and at least one seat are required.", now);
        }

        if (request.SeatIds.Any(id => id == Guid.Empty) || request.SeatIds.Distinct().Count() != request.SeatIds.Count)
        {
            return Failure("INVALID_REQUEST", "Seat identifiers must be non-empty and unique.", now);
        }

        return null;
    }

    private static bool IsRetryable(Exception exception)
    {
        var postgresException = exception as PostgresException
            ?? exception.InnerException as PostgresException;
        return postgresException?.SqlState is PostgresErrorCodes.SerializationFailure
            or PostgresErrorCodes.DeadlockDetected
            or PostgresErrorCodes.UniqueViolation;
    }

    private DateTime GetUtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private static SeatHoldResultDto Failure(string code, string message, DateTime now)
    {
        return new SeatHoldResultDto
        {
            Success = false,
            ErrorCode = code,
            Message = message,
            ServerTimeUtc = now
        };
    }

    private static SeatHoldResultDto BuildResult(
        bool success,
        string message,
        Guid holdGroupId,
        Guid showtimeId,
        IReadOnlyList<Guid> seatIds,
        string status,
        DateTime expiredAt,
        DateTime serverTimeUtc)
    {
        return new SeatHoldResultDto
        {
            Success = success,
            Message = message,
            HoldGroupId = holdGroupId,
            ShowtimeId = showtimeId,
            SeatIds = seatIds,
            Status = status,
            ExpiredAt = expiredAt,
            ServerTimeUtc = serverTimeUtc
        };
    }

    private void LogOperation(
        string operation,
        Guid? holdGroupId,
        Guid showtimeId,
        int seatCount,
        string result,
        TimeSpan duration)
    {
        _logger.LogInformation(
            "Seat hold {Operation} completed. HoldGroupId={HoldGroupId}, ShowtimeId={ShowtimeId}, SeatCount={SeatCount}, Result={Result}, DurationMs={DurationMs}, TimestampUtc={TimestampUtc}",
            operation,
            holdGroupId,
            showtimeId,
            seatCount,
            result,
            duration.TotalMilliseconds,
            GetUtcNow());
    }
}
