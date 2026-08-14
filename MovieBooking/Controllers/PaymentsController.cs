using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Constants;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Controllers;

[Route("api/payments")]
public class PaymentsController : CrudController<Payment, PaymentDto>
{
    private readonly IVnPayService _vnPayService;
    private readonly IPaymentWorkflowService _paymentWorkflowService;
    private readonly ISeatRealtimePublisher _seatRealtimePublisher;
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public PaymentsController(
        IPaymentService crudService,
        IVnPayService vnPayService,
        IPaymentWorkflowService paymentWorkflowService,
        ISeatRealtimePublisher seatRealtimePublisher,
        AppDbContext db,
        IConfiguration configuration) : base(crudService)
    {
        _vnPayService = vnPayService;
        _paymentWorkflowService = paymentWorkflowService;
        _seatRealtimePublisher = seatRealtimePublisher;
        _db = db;
        _configuration = configuration;
    }

    [Authorize(Roles = "Admin")]
    public override Task<ActionResult<IReadOnlyList<PaymentDto>>> GetAll(
        CancellationToken cancellationToken) => base.GetAll(cancellationToken);

    [Authorize]
    public override async Task<ActionResult<PaymentDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var payment = await _db.Payments
            .AsNoTracking()
            .Include(item => item.Booking)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (payment == null)
        {
            return NotFound();
        }

        if (!CanAccessBooking(payment.Booking))
        {
            return Forbid();
        }

        return Ok(new PaymentDto
        {
            Id = payment.Id,
            BookingId = payment.BookingId,
            Amount = payment.Amount,
            Method = payment.Method,
            Status = payment.Status,
            TransactionCode = string.Empty
        });
    }

    public override Task<ActionResult<PaymentDto>> Create(
        PaymentDto dto,
        CancellationToken cancellationToken) =>
        Task.FromResult<ActionResult<PaymentDto>>(StatusCode(StatusCodes.Status405MethodNotAllowed));

    public override Task<IActionResult> Update(
        Guid id,
        PaymentDto dto,
        CancellationToken cancellationToken) =>
        Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status405MethodNotAllowed));

    public override Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status405MethodNotAllowed));

    [HttpPost("create-url")]
    public async Task<IActionResult> CreatePaymentUrl(
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(
            item => item.Id == request.BookingId,
            cancellationToken);
        if (booking == null)
        {
            return NotFound(new { message = "Booking was not found." });
        }

        if (!CanAccessBooking(booking))
        {
            return Forbid();
        }

        if (booking.Channel != BookingChannels.CustomerOnline
            || booking.Status != BookingStatuses.Pending)
        {
            return Conflict(new { message = "Booking is not eligible for online payment." });
        }

        var payment = await _db.Payments.SingleOrDefaultAsync(
            item => item.BookingId == booking.Id,
            cancellationToken);
        if (payment != null && payment.Status != PaymentStatuses.Pending)
        {
            return Conflict(new { message = "Payment is already finalized." });
        }

        if (payment == null)
        {
            payment = new Payment
            {
                BookingId = booking.Id,
                Amount = booking.TotalPrice,
                Method = PaymentMethods.VnPay,
                Status = PaymentStatuses.Pending,
                TransactionCode = string.Empty
            };
            _db.Payments.Add(payment);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        return Ok(new { Url = _vnPayService.CreatePaymentUrl(ipAddress, payment, booking) });
    }

    [HttpGet("vnpay-ipn")]
    [AllowAnonymous]
    public async Task<IActionResult> VnPayIpn(CancellationToken cancellationToken)
    {
        var fields = Request.Query.ToDictionary(item => item.Key, item => item.Value.ToString());
        var response = _vnPayService.PaymentExecute(fields);
        if (!response.Success)
        {
            return Ok(new { RspCode = "97", Message = "Invalid signature or merchant." });
        }

        if (!Guid.TryParse(response.OrderId, out var paymentId))
        {
            return Ok(new { RspCode = "01", Message = "Payment was not found." });
        }

        var payment = await _db.Payments
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == paymentId, cancellationToken);
        if (payment == null)
        {
            return Ok(new { RspCode = "01", Message = "Payment was not found." });
        }

        if (response.Amount != payment.Amount || response.CurrencyCode != "VND")
        {
            return Ok(new { RspCode = "04", Message = "Invalid payment amount." });
        }

        if (string.IsNullOrWhiteSpace(response.TransactionStatus)
            || string.IsNullOrWhiteSpace(response.VnPayResponseCode))
        {
            return Ok(new { RspCode = "02", Message = "Incomplete provider status." });
        }

        var succeeded = response.VnPayResponseCode == VnPayStatuses.Success
            && response.TransactionStatus == VnPayStatuses.Success;
        var confirmedFailure = VnPayStatuses.ConfirmedFailureResponseCodes.Contains(
                response.VnPayResponseCode)
            && VnPayStatuses.ConfirmedFailureTransactionStatuses.Contains(
                response.TransactionStatus);
        if (!succeeded && !confirmedFailure)
        {
            return Ok(new { RspCode = "02", Message = "Conflicting provider status." });
        }
        var command = new ProviderPaymentCommandDto
        {
            PaymentId = paymentId,
            ProviderEventKey = BuildProviderEventKey(response, paymentId),
            ProviderTransactionCode = response.TransactionId,
            Succeeded = succeeded,
            ConfirmedFailure = confirmedFailure
        };

        var result = await _paymentWorkflowService.ProcessProviderNotificationAsync(
            command,
            cancellationToken);
        if (result.ChangeBatch != null)
        {
            await _seatRealtimePublisher.PublishAsync(result.ChangeBatch, cancellationToken);
        }

        if (!result.Success)
        {
            return Ok(new { RspCode = "02", Message = "Payment state conflict." });
        }

        return Ok(new { RspCode = "00", Message = "Confirm success." });
    }

    [HttpGet("vnpay-return")]
    [AllowAnonymous]
    public async Task<IActionResult> VnPayReturn(CancellationToken cancellationToken)
    {
        var fields = Request.Query.ToDictionary(item => item.Key, item => item.Value.ToString());
        var response = _vnPayService.PaymentExecute(fields);
        Guid? bookingId = null;
        if (response.Success && Guid.TryParse(response.OrderId, out var paymentId))
        {
            bookingId = await _db.Payments
                .AsNoTracking()
                .Where(item => item.Id == paymentId)
                .Select(item => (Guid?)item.BookingId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var baseUrl = _configuration["VnPay:AppReturnUrl"]
            ?? "https://datn-iuj8.onrender.com/payment-result";
        return bookingId.HasValue
            ? Redirect($"{baseUrl}?bookingId={bookingId.Value}")
            : Redirect(baseUrl);
    }

    [HttpPost("{id:guid}/refund")]
    [MovieBooking.Infrastructure.Security.HasPermission("Refund")]
    public IActionResult RefundPayment(Guid id) => StatusCode(
        StatusCodes.Status501NotImplemented,
        new { message = "VNPAY refund integration is not configured." });

    private bool CanAccessBooking(Booking booking)
    {
        if (User.IsInRole("Admin") || User.IsInRole("Manager") || User.IsInRole("Cashier"))
        {
            return true;
        }

        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return claim != null
            && Guid.TryParse(claim.Value, out var userId)
            && booking.UserId == userId;
    }

    private static string BuildProviderEventKey(VnPayResponseModel response, Guid paymentId)
    {
        var canonical = string.Join('|',
            "VNPAY",
            response.TerminalCode,
            paymentId.ToString("N"),
            response.VnPayResponseCode,
            response.TransactionStatus,
            response.Amount.ToString("0.##", CultureInfo.InvariantCulture),
            string.IsNullOrEmpty(response.CurrencyCode) ? "VND" : response.CurrencyCode,
            response.TransactionId);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }
}

public sealed class CreatePaymentRequest
{
    public Guid BookingId { get; init; }
}
