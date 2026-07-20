using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;
using MovieBooking.Infrastructure.Security;
using System.Security.Claims;
using System.Data;

namespace MovieBooking.Controllers;

[Route("api/payments")]
public class PaymentsController : CrudController<Payment, PaymentDto>
{
    private readonly IVnPayService _vnPayService;
    private readonly AppDbContext _db;
    private readonly ILoyaltyService _loyaltyService;

    public PaymentsController(
        ICrudService<Payment, PaymentDto> crudService, 
        IVnPayService vnPayService, 
        AppDbContext db,
        ILoyaltyService loyaltyService) : base(crudService) 
    { 
        _vnPayService = vnPayService;
        _db = db;
        _loyaltyService = loyaltyService;
    }

    [Authorize(Roles = "Admin")]
    public override Task<ActionResult<IReadOnlyList<PaymentDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        return base.GetAll(cancellationToken);
    }

    [Authorize(Roles = "Admin")]
    public override Task<ActionResult<PaymentDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        return base.GetById(id, cancellationToken);
    }

    [HttpPost("create-url")]
    public async Task<IActionResult> CreatePaymentUrl(
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(
            b => b.Id == request.BookingId,
            cancellationToken);
        if (booking == null) return NotFound("Booking not found");
        if (!CanAccessBooking(booking)) return Forbid();
        if (booking.Status != "Pending") return BadRequest("Booking is not in pending status.");

        var existingPayment = await _db.Payments
            .FirstOrDefaultAsync(
                p => p.BookingId == booking.Id && p.Status == "Pending",
                cancellationToken);
        if (existingPayment != null)
        {
            var existingIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var existingUrl = _vnPayService.CreatePaymentUrl(existingIpAddress, existingPayment, booking);
            return Ok(new { Url = existingUrl });
        }

        var payment = new Payment
        {
            BookingId = booking.Id,
            Amount = booking.TotalPrice,
            Method = "VNPAY",
            Status = "Pending",
            TransactionCode = string.Empty
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var url = _vnPayService.CreatePaymentUrl(ipAddress, payment, booking);
        return Ok(new { Url = url });
    }

    [HttpGet("vnpay-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> PaymentCallback(CancellationToken cancellationToken)
    {
        var dict = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
        var response = _vnPayService.PaymentExecute(dict);

        if (!Guid.TryParse(response.OrderId, out var paymentId))
        {
            return BadRequest("Invalid PaymentId");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var payment = await _db.Payments
            .Include(p => p.Booking)
            .ThenInclude(b => b.Tickets)
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        if (payment == null) return NotFound("Payment not found");

        var callbackSucceeded = response.Success
            && response.VnPayResponseCode == "00"
            && response.Amount == payment.Amount;
        if (payment.Status == "Success")
        {
            return Redirect(BuildReturnUrl(true, payment.BookingId));
        }

        if (payment.Status != "Pending")
        {
            return Conflict("Payment is no longer pending.");
        }

        if (callbackSucceeded)
        {
            payment.Status = "Success";
            payment.TransactionCode = response.TransactionId;
            payment.Booking.Status = "Paid";
            payment.Booking.ExpiredAt = null;

            foreach (var ticket in payment.Booking.Tickets)
            {
                ticket.Status = "Reserved"; // Ensure ticket is reserved
            }

            await _loyaltyService.EarnForBookingAsync(
                payment.BookingId,
                payment.Amount,
                cancellationToken);
        }
        else
        {
            payment.Status = "Failed";
            payment.Booking.Status = "Failed";
        }

        _db.PaymentLogs.Add(new PaymentLog
        {
            PaymentId = payment.Id,
            Status = callbackSucceeded ? "Success" : "Failed",
            ResponseData = $"VNPAY Response: {response.VnPayResponseCode}, Transaction: {response.TransactionId}"
        });

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Redirect(BuildReturnUrl(callbackSucceeded, payment.BookingId));
    }
    [HttpPost("{id}/refund")]
    [HasPermission("Refund")]
    public async Task<IActionResult> RefundPayment(Guid id, CancellationToken cancellationToken)
    {
        var payment = await _db.Payments
            .Include(p => p.Booking)
            .ThenInclude(b => b.Tickets)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (payment == null) return NotFound("Payment not found");
        if (payment.Status != "Success") return BadRequest("Cannot refund a non-successful payment.");

        return StatusCode(
            StatusCodes.Status501NotImplemented,
            new { message = "VNPAY refund integration is not configured." });
    }

    private bool CanAccessBooking(Booking booking)
    {
        if (User.IsInRole("Admin") || User.IsInRole("Manager") || User.IsInRole("Cashier"))
        {
            return true;
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return userIdClaim != null
            && Guid.TryParse(userIdClaim.Value, out var userId)
            && booking.UserId == userId;
    }

    private string BuildReturnUrl(bool success, Guid bookingId)
    {
        var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var baseUrl = config["VnPay:AppReturnUrl"] ?? "http://localhost:3000/payment-result";
        return $"{baseUrl}?success={success.ToString().ToLowerInvariant()}&bookingId={bookingId}";
    }
}

public class CreatePaymentRequest
{
    public Guid BookingId { get; set; }
}
