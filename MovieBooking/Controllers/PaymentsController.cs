using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Controllers;

[Route("api/payments")]
public class PaymentsController : CrudController<Payment, PaymentDto>
{
    private readonly IVnPayService _vnPayService;
    private readonly AppDbContext _db;

    public PaymentsController(
        ICrudService<Payment, PaymentDto> crudService, 
        IVnPayService vnPayService, 
        AppDbContext db) : base(crudService) 
    { 
        _vnPayService = vnPayService;
        _db = db;
    }

    [HttpPost("create-url")]
    public async Task<IActionResult> CreatePaymentUrl([FromBody] CreatePaymentRequest request)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == request.BookingId);
        if (booking == null) return NotFound("Booking not found");
        if (booking.Status != "Pending") return BadRequest("Booking is not in pending status.");

        var payment = new Payment
        {
            BookingId = booking.Id,
            Amount = booking.TotalPrice,
            Method = "VNPAY",
            Status = "Pending",
            TransactionCode = string.Empty
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var url = _vnPayService.CreatePaymentUrl(ipAddress, payment, booking);
        return Ok(new { Url = url });
    }

    [HttpGet("vnpay-callback")]
    public async Task<IActionResult> PaymentCallback()
    {
        var dict = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
        var response = _vnPayService.PaymentExecute(dict);

        if (!Guid.TryParse(response.OrderId, out var paymentId))
        {
            return BadRequest("Invalid PaymentId");
        }

        var payment = await _db.Payments.Include(p => p.Booking).ThenInclude(b => b.Tickets).FirstOrDefaultAsync(p => p.Id == paymentId);
        if (payment == null) return NotFound("Payment not found");

        if (response.Success && response.VnPayResponseCode == "00")
        {
            payment.Status = "Success";
            payment.TransactionCode = response.TransactionId;
            payment.Booking.Status = "Paid";
            payment.Booking.ExpiredAt = null;

            foreach (var ticket in payment.Booking.Tickets)
            {
                ticket.Status = "Reserved"; // Ensure ticket is reserved
            }

            // Loyalty Points Logic
            var user = await _db.Users.Include(u => u.LoyaltyPoint).FirstOrDefaultAsync(u => u.Id == payment.Booking.UserId);
            if (user != null)
            {
                int earnedPoints = (int)(payment.Amount * 0.01m); // 1% points
                if (user.LoyaltyPoint == null)
                {
                    user.LoyaltyPoint = new LoyaltyPoint { UserId = user.Id, Points = earnedPoints };
                    _db.LoyaltyPoints.Add(user.LoyaltyPoint);
                }
                else
                {
                    user.LoyaltyPoint.Points += earnedPoints;
                }
                
                _db.PointTransactions.Add(new PointTransaction
                {
                    UserId = user.Id,
                    Points = earnedPoints,
                    Type = "Earn"
                });
            }
        }
        else
        {
            payment.Status = "Failed";
            payment.Booking.Status = "Failed";
        }

        _db.PaymentLogs.Add(new PaymentLog
        {
            PaymentId = payment.Id,
            Status = response.Success ? "Success" : "Failed",
            ResponseData = $"VNPAY Response: {response.VnPayResponseCode}, Transaction: {response.TransactionId}"
        });

        await _db.SaveChangesAsync();

        var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var baseUrl = config["VnPay:AppReturnUrl"] ?? "http://localhost:3000/payment-result";
        var returnUrl = $"{baseUrl}?success={response.Success}&bookingId={payment.BookingId}";
        return Redirect(returnUrl);
    }
    [HttpPost("{id}/refund")]
    public async Task<IActionResult> RefundPayment(Guid id)
    {
        var payment = await _db.Payments.Include(p => p.Booking).ThenInclude(b => b.Tickets).FirstOrDefaultAsync(p => p.Id == id);
        if (payment == null) return NotFound("Payment not found");
        if (payment.Status != "Success") return BadRequest("Cannot refund a non-successful payment.");

        // Simulate calling VNPAY Refund API here
        bool refundSuccess = true; // Simulating success for Sandbox
        
        if (refundSuccess)
        {
            payment.Status = "Refunded";
            payment.Booking.Status = "Refunded";
            foreach (var ticket in payment.Booking.Tickets)
            {
                ticket.Status = "Cancelled";
            }

            // Revert Loyalty Points Logic
            var user = await _db.Users.Include(u => u.LoyaltyPoint).FirstOrDefaultAsync(u => u.Id == payment.Booking.UserId);
            if (user != null && user.LoyaltyPoint != null)
            {
                int refundedPoints = (int)(payment.Amount * 0.01m); // 1% points
                user.LoyaltyPoint.Points -= refundedPoints;
                if (user.LoyaltyPoint.Points < 0) user.LoyaltyPoint.Points = 0;

                _db.PointTransactions.Add(new PointTransaction
                {
                    UserId = user.Id,
                    Points = -refundedPoints,
                    Type = "Refund"
                });
            }

            _db.PaymentLogs.Add(new PaymentLog
            {
                PaymentId = payment.Id,
                Status = "Success",
                ResponseData = $"VNPAY Refund processed successfully"
            });

            await _db.SaveChangesAsync();
            return Ok(new { Message = "Refund successful" });
        }

        return BadRequest("Refund failed at Payment Gateway.");
    }
}

public class CreatePaymentRequest
{
    public Guid BookingId { get; set; }
}
