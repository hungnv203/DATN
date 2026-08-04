using System.Data;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Constants;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;
using Npgsql;
using PaymentEntity = MovieBooking.Domain.Entities.Payment;

namespace MovieBooking.Infrastructure.Services;

internal sealed class PaymentWorkflowService : IPaymentWorkflowService
{
    private static readonly HashSet<string> CancellationReasons =
        ["CustomerCancelled", "OperatorCancelled"];

    private readonly AppDbContext _db;
    private readonly IMapper _mapper;
    private readonly ILoyaltyService _loyaltyService;
    private readonly TimeProvider _timeProvider;

    public PaymentWorkflowService(
        AppDbContext db,
        IMapper mapper,
        ILoyaltyService loyaltyService,
        TimeProvider timeProvider)
    {
        _db = db;
        _mapper = mapper;
        _loyaltyService = loyaltyService;
        _timeProvider = timeProvider;
    }

    public Task<PaymentTransitionResultDto> ConfirmPosCashAsync(
        Guid actorUserId,
        Guid bookingId,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default) =>
        ExecuteWithRetryAsync(
            () => ConfirmPosCashCoreAsync(
                actorUserId,
                bookingId,
                idempotencyKey,
                cancellationToken),
            cancellationToken);

    private async Task<PaymentTransitionResultDto> ConfirmPosCashCoreAsync(
        Guid actorUserId,
        Guid bookingId,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var fingerprint = $"{bookingId:N}|{PaymentMethods.Cash}";
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var booking = await LockBookingAsync(bookingId, cancellationToken);
        if (booking == null)
        {
            return Failure("BOOKING_NOT_FOUND");
        }

        var replay = await FindClientReplayAsync(
            idempotencyKey,
            bookingId,
            fingerprint,
            cancellationToken);
        if (replay != null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return replay;
        }

        if (booking.Channel != BookingChannels.PointOfSale || booking.UserId != actorUserId)
        {
            return Failure("POS_BOOKING_CONFLICT");
        }

        if (booking.Status == BookingStatuses.Paid)
        {
            return Failure("ALREADY_PAID");
        }

        if (booking.Status != BookingStatuses.Pending)
        {
            return Failure("BOOKING_NOT_PENDING");
        }

        var now = UtcNow();
        var holds = await LoadActiveBookingHoldsAsync(booking, now, cancellationToken);
        if (holds.Count == 0 || holds.Count != booking.Tickets.Count)
        {
            return Failure("HOLD_NOT_ACTIVE");
        }

        var payment = new PaymentEntity
        {
            BookingId = booking.Id,
            Amount = booking.TotalPrice,
            Method = PaymentMethods.Cash,
            Status = PaymentStatuses.Success,
            TransactionCode = string.Empty
        };
        _db.Payments.Add(payment);
        ApplySuccess(booking, holds, now);
        var batch = await CreateBatchAsync(booking, holds, "Booked", now, cancellationToken);
        _db.PaymentOperations.Add(CreateClientOperation(
            booking,
            payment,
            idempotencyKey,
            PaymentOperationTypes.PosConfirmation,
            PaymentMethods.Cash,
            fingerprint,
            PaymentOperationResults.Completed,
            "PAID",
            actorUserId,
            now));
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Success(booking, "Paid", batch);
    }

    public Task<PaymentTransitionResultDto> CancelPosAsync(
        Guid actorUserId,
        Guid bookingId,
        Guid idempotencyKey,
        string reasonCode,
        CancellationToken cancellationToken = default) =>
        ExecuteWithRetryAsync(
            () => CancelPosCoreAsync(
                actorUserId,
                bookingId,
                idempotencyKey,
                reasonCode,
                cancellationToken),
            cancellationToken);

    private async Task<PaymentTransitionResultDto> CancelPosCoreAsync(
        Guid actorUserId,
        Guid bookingId,
        Guid idempotencyKey,
        string reasonCode,
        CancellationToken cancellationToken = default)
    {
        if (!CancellationReasons.Contains(reasonCode))
        {
            return Failure("INVALID_REASON_CODE");
        }

        var fingerprint = $"{bookingId:N}|Cancel|{reasonCode}";
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var booking = await LockBookingAsync(bookingId, cancellationToken);
        if (booking == null)
        {
            return Failure("BOOKING_NOT_FOUND");
        }

        var replay = await FindClientReplayAsync(
            idempotencyKey,
            bookingId,
            fingerprint,
            cancellationToken);
        if (replay != null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return replay;
        }

        if (booking.Channel != BookingChannels.PointOfSale || booking.UserId != actorUserId)
        {
            return Failure("POS_BOOKING_CONFLICT");
        }

        if (booking.Status != BookingStatuses.Pending)
        {
            return Failure("BOOKING_NOT_PENDING");
        }

        var now = UtcNow();
        var holds = await LoadActiveBookingHoldsAsync(booking, now, cancellationToken);
        ApplyCancellation(booking, holds, BookingStatuses.Cancelled, now);
        var batch = holds.Count == 0
            ? null
            : await CreateBatchAsync(booking, holds, "Available", now, cancellationToken);
        _db.PaymentOperations.Add(CreateClientOperation(
            booking,
            null,
            idempotencyKey,
            PaymentOperationTypes.PosCancellation,
            PaymentMethods.Cash,
            fingerprint,
            PaymentOperationResults.Completed,
            reasonCode,
            actorUserId,
            now));
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Success(booking, "Cancelled", batch);
    }

    public Task<PaymentTransitionResultDto> ProcessProviderNotificationAsync(
        ProviderPaymentCommandDto command,
        CancellationToken cancellationToken = default) =>
        ExecuteWithRetryAsync(
            () => ProcessProviderNotificationCoreAsync(command, cancellationToken),
            cancellationToken);

    private async Task<PaymentTransitionResultDto> ProcessProviderNotificationCoreAsync(
        ProviderPaymentCommandDto command,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var payment = await _db.Payments
            .FromSqlInterpolated(
                $"SELECT * FROM \"Payments\" WHERE \"Id\" = {command.PaymentId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (payment == null)
        {
            return Failure("PAYMENT_NOT_FOUND");
        }


        var replay = await _db.PaymentOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                operation => operation.ProviderEventKey == command.ProviderEventKey,
                cancellationToken);
        if (replay != null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await CurrentResultAsync(replay.BookingId, true, cancellationToken);
        }

        var booking = await LockBookingAsync(payment.BookingId, cancellationToken);
        if (booking == null || booking.Channel != BookingChannels.CustomerOnline)
        {
            return Failure("PAYMENT_BOOKING_CONFLICT");
        }

        var now = UtcNow();
        if (command.Succeeded)
        {
            if (payment.Status == PaymentStatuses.Success && booking.Status == BookingStatuses.Paid)
            {
                _db.PaymentOperations.Add(CreateProviderOperation(
                    booking,
                    payment,
                    command,
                    PaymentOperationResults.Completed,
                    "PAID_REPLAY",
                    now));
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Success(booking, "Paid", null, true);
            }

            if (payment.Status != PaymentStatuses.Pending || booking.Status != BookingStatuses.Pending)
            {
                _db.PaymentOperations.Add(CreateProviderOperation(
                    booking,
                    payment,
                    command,
                    PaymentOperationResults.ReviewRequired,
                    "PAYMENT_REVIEW_REQUIRED",
                    now));
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Success(booking, "ReviewRequired", null);
            }

            var holds = await LoadActiveBookingHoldsAsync(booking, now, cancellationToken);
            if (holds.Count == 0 || holds.Count != booking.Tickets.Count)
            {
                _db.PaymentOperations.Add(CreateProviderOperation(
                    booking,
                    payment,
                    command,
                    PaymentOperationResults.ReviewRequired,
                    "PAYMENT_REVIEW_REQUIRED",
                    now));
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Success(booking, "ReviewRequired", null);
            }

            payment.Status = PaymentStatuses.Success;
            payment.TransactionCode = command.ProviderTransactionCode;
            ApplySuccess(booking, holds, now);
            await _loyaltyService.EarnForBookingAsync(
                booking.Id,
                payment.Amount,
                cancellationToken);
            var batch = await CreateBatchAsync(booking, holds, "Booked", now, cancellationToken);
            _db.PaymentOperations.Add(CreateProviderOperation(
                booking,
                payment,
                command,
                PaymentOperationResults.Completed,
                "PAID",
                now));
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Success(booking, "Paid", batch);
        }

        if (!command.ConfirmedFailure)
        {
            return Failure("UNCONFIRMED_PROVIDER_RESULT");
        }

        if (payment.Status == PaymentStatuses.Failed && booking.Status == BookingStatuses.Failed)
        {
            _db.PaymentOperations.Add(CreateProviderOperation(
                booking,
                payment,
                command,
                PaymentOperationResults.Failed,
                "FAILED_REPLAY",
                now));
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Success(booking, "Failed", null, true);
        }

        if (payment.Status != PaymentStatuses.Pending || booking.Status != BookingStatuses.Pending)
        {
            _db.PaymentOperations.Add(CreateProviderOperation(
                booking,
                payment,
                command,
                PaymentOperationResults.ReviewRequired,
                "PAYMENT_REVIEW_REQUIRED",
                now));
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Success(booking, "ReviewRequired", null);
        }

        var failedHolds = await LoadActiveBookingHoldsAsync(booking, now, cancellationToken);
        payment.Status = PaymentStatuses.Failed;
        ApplyCancellation(booking, failedHolds, BookingStatuses.Failed, now);
        await _loyaltyService.ReturnRedeemedPointsAsync(booking.Id, cancellationToken);
        var failureBatch = failedHolds.Count == 0
            ? null
            : await CreateBatchAsync(booking, failedHolds, "Available", now, cancellationToken);
        _db.PaymentOperations.Add(CreateProviderOperation(
            booking,
            payment,
            command,
            PaymentOperationResults.Failed,
            "PAYMENT_FAILED",
            now));
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Success(booking, "Failed", failureBatch);
    }

    private async Task<Booking?> LockBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var booking = await _db.Bookings
            .FromSqlInterpolated(
                $"SELECT * FROM \"Bookings\" WHERE \"Id\" = {bookingId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (booking == null)
        {
            return null;
        }

        await _db.Entry(booking).Collection(item => item.Tickets).LoadAsync(cancellationToken);
        return booking;
    }

    private async Task<List<SeatHold>> LoadActiveBookingHoldsAsync(
        Booking booking,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!booking.SeatHoldGroupId.HasValue)
        {
            return [];
        }

        return await _db.SeatHolds
            .Where(hold => hold.BookingId == booking.Id
                           && hold.HoldGroupId == booking.SeatHoldGroupId.Value
                           && hold.UserId == booking.UserId
                           && hold.ShowtimeId == booking.ShowtimeId
                           && hold.Status == SeatHoldStatuses.Active
                           && hold.ExpiredAt > now)
            .OrderBy(hold => hold.SeatId)
            .ToListAsync(cancellationToken);
    }

    private static void ApplySuccess(Booking booking, IEnumerable<SeatHold> holds, DateTime now)
    {
        booking.Status = BookingStatuses.Paid;
        booking.ExpiredAt = null;
        booking.MarkUpdated(new DateTimeOffset(now, TimeSpan.Zero));
        foreach (var ticket in booking.Tickets)
        {
            ticket.Status = TicketStatuses.Booked;
            ticket.MarkUpdated(new DateTimeOffset(now, TimeSpan.Zero));
        }

        foreach (var hold in holds)
        {
            hold.Status = SeatHoldStatuses.Completed;
            hold.CompletedAt = now;
            hold.MarkUpdated(new DateTimeOffset(now, TimeSpan.Zero));
        }
    }

    private static void ApplyCancellation(
        Booking booking,
        IEnumerable<SeatHold> holds,
        string bookingStatus,
        DateTime now)
    {
        booking.Status = bookingStatus;
        booking.ExpiredAt = null;
        booking.MarkUpdated(new DateTimeOffset(now, TimeSpan.Zero));
        foreach (var ticket in booking.Tickets)
        {
            ticket.Status = TicketStatuses.Cancelled;
            ticket.MarkUpdated(new DateTimeOffset(now, TimeSpan.Zero));
        }

        foreach (var hold in holds)
        {
            hold.Status = SeatHoldStatuses.Released;
            hold.ReleasedAt = now;
            hold.MarkUpdated(new DateTimeOffset(now, TimeSpan.Zero));
        }
    }

    private async Task<SeatStateChangeBatchDto> CreateBatchAsync(
        Booking booking,
        IReadOnlyList<SeatHold> holds,
        string status,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var version = await ShowtimeSeatVersionStore.IncrementAsync(
            _db,
            booking.ShowtimeId,
            cancellationToken);
        return new SeatStateChangeBatchDto
        {
            ShowtimeId = booking.ShowtimeId,
            Version = version,
            CommittedAtUtc = now,
            Changes = holds.Select(hold => new SeatStateChangeDto
            {
                SeatId = hold.SeatId,
                Status = status,
                HoldGroupId = hold.HoldGroupId
            }).ToArray()
        };
    }

    private async Task<PaymentTransitionResultDto?> FindClientReplayAsync(
        Guid key,
        Guid bookingId,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var operation = await _db.PaymentOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.ClientIdempotencyKey == key, cancellationToken);
        if (operation == null)
        {
            return null;
        }

        if (operation.BookingId != bookingId || operation.RequestFingerprint != fingerprint)
        {
            return Failure("IDEMPOTENCY_CONFLICT");
        }

        return await CurrentResultAsync(bookingId, true, cancellationToken);
    }

    private async Task<PaymentTransitionResultDto> CurrentResultAsync(
        Guid bookingId,
        bool replay,
        CancellationToken cancellationToken)
    {
        var booking = await _db.Bookings
            .AsNoTracking()
            .Include(item => item.Tickets)
            .SingleOrDefaultAsync(item => item.Id == bookingId, cancellationToken);
        return booking == null
            ? Failure("BOOKING_NOT_FOUND")
            : Success(booking, booking.Status, null, replay);
    }

    private static PaymentOperation CreateClientOperation(
        Booking booking,
        PaymentEntity? payment,
        Guid key,
        string type,
        string method,
        string fingerprint,
        string result,
        string reason,
        Guid actor,
        DateTime now) => new()
    {
        BookingId = booking.Id,
        PaymentId = payment?.Id,
        ClientIdempotencyKey = key,
        OperationType = type,
        Method = method,
        RequestFingerprint = fingerprint,
        Result = result,
        ReasonCode = reason,
        ActorUserId = actor,
        CompletedAtUtc = now
    };

    private static PaymentOperation CreateProviderOperation(
        Booking booking,
        PaymentEntity payment,
        ProviderPaymentCommandDto command,
        string result,
        string reason,
        DateTime now) => new()
    {
        BookingId = booking.Id,
        PaymentId = payment.Id,
        ProviderEventKey = command.ProviderEventKey,
        OperationType = PaymentOperationTypes.ProviderNotification,
        Method = PaymentMethods.VnPay,
        RequestFingerprint = command.ProviderEventKey,
        Result = result,
        ReasonCode = reason,
        CompletedAtUtc = now
    };

    private PaymentTransitionResultDto Success(
        Booking booking,
        string paymentState,
        SeatStateChangeBatchDto? batch,
        bool replay = false) => new()
    {
        Success = true,
        IsReplay = replay,
        PaymentState = paymentState,
        Booking = _mapper.Map<BookingDto>(booking),
        ChangeBatch = batch
    };

    private static PaymentTransitionResultDto Failure(string code) => new()
    {
        Success = false,
        ErrorCode = code,
        PaymentState = "Conflict"
    };

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private async Task<PaymentTransitionResultDto> ExecuteWithRetryAsync(
        Func<Task<PaymentTransitionResultDto>> action,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception exception) when (IsRetryable(exception) && attempt < maxAttempts)
            {
                _db.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
            catch (Exception exception) when (IsRetryable(exception))
            {
                _db.ChangeTracker.Clear();
                return Failure("CONCURRENCY_CONFLICT");
            }
        }

        return Failure("CONCURRENCY_CONFLICT");
    }

    private static bool IsRetryable(Exception exception)
    {
        var postgres = exception as PostgresException
            ?? exception.InnerException as PostgresException;
        return postgres?.SqlState is PostgresErrorCodes.SerializationFailure
            or PostgresErrorCodes.DeadlockDetected
            or PostgresErrorCodes.UniqueViolation;
    }
}
