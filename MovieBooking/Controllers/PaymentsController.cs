using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Constants;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/payments")]
public class PaymentsController : CrudController<Payment, PaymentDto>
{
    private readonly IVnPayService _vnPayService;
    private readonly IPaymentWorkflowService _paymentWorkflowService;
    private readonly ISeatRealtimePublisher _seatRealtimePublisher;
    private readonly IConfiguration _configuration;
    private readonly IPaymentService _paymentService;

    public PaymentsController(
        IPaymentService crudService,
        IVnPayService vnPayService,
        IPaymentWorkflowService paymentWorkflowService,
        ISeatRealtimePublisher seatRealtimePublisher,
        IConfiguration configuration) : base(crudService)
    {
        _paymentService = crudService;
        _vnPayService = vnPayService;
        _paymentWorkflowService = paymentWorkflowService;
        _seatRealtimePublisher = seatRealtimePublisher;
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
        Guid? currentUserId = null;
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (claim != null && Guid.TryParse(claim.Value, out var userId))
        {
            currentUserId = userId;
        }

        var isAdminOrManager = User.IsInRole("Admin") || User.IsInRole("Manager") || User.IsInRole("Cashier");

        var (dto, found, authorized) = await _paymentService.GetPaymentDetailForUserAsync(
            id,
            currentUserId,
            isAdminOrManager,
            cancellationToken);

        if (!found)
        {
            return NotFound();
        }

        if (!authorized)
        {
            return Forbid();
        }

        return Ok(dto);
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
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        
        Guid? currentUserId = null;
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (claim != null && Guid.TryParse(claim.Value, out var userId))
        {
            currentUserId = userId;
        }

        var isAdminOrManager = User.IsInRole("Admin") || User.IsInRole("Manager") || User.IsInRole("Cashier");

        var result = await _paymentService.CreatePaymentUrlAsync(
            request.BookingId,
            ipAddress,
            currentUserId,
            isAdminOrManager,
            cancellationToken);

        if (!result.Success)
        {
            if (result.Message == "Booking was not found.") return NotFound(new { message = result.Message });
            if (result.Message == "Forbidden") return Forbid();
            return Conflict(new { message = result.Message });
        }

        return Ok(new { Url = result.Url });
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

        var (found, amount) = await _paymentService.GetPaymentVerificationInfoAsync(paymentId, cancellationToken);
        if (!found)
        {
            return Ok(new { RspCode = "01", Message = "Payment was not found." });
        }

        if (response.Amount != amount || response.CurrencyCode != "VND")
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
            bookingId = await _paymentService.GetBookingIdByPaymentIdAsync(paymentId, cancellationToken);

            var succeeded = response.VnPayResponseCode == VnPayStatuses.Success
                && response.TransactionStatus == VnPayStatuses.Success;
            var confirmedFailure = VnPayStatuses.ConfirmedFailureResponseCodes.Contains(response.VnPayResponseCode)
                && VnPayStatuses.ConfirmedFailureTransactionStatuses.Contains(response.TransactionStatus);

            if (succeeded || confirmedFailure)
            {
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
            }
        }

        var isSuccess = response.Success && response.VnPayResponseCode == "00";
        var statusTitle = isSuccess ? "Thanh toán thành công!" : "Thanh toán không thành công";
        var statusDesc = isSuccess
            ? "Giao dịch của bạn đã được ghi nhận thành công. Cửa sổ này sẽ tự động đóng."
            : "Giao dịch đã bị hủy hoặc xảy ra lỗi trong quá trình thanh toán. Cửa sổ này sẽ tự động đóng.";
        if (Request.Headers.Accept.ToString().Contains("application/json"))
        {
            return Ok(new
            {
                Success = isSuccess,
                BookingId = bookingId,
                ResponseCode = response.VnPayResponseCode,
                TransactionStatus = response.TransactionStatus
            });
        }

        var icon = isSuccess ? "✅" : "⚠️";
        var deepLink = $"moviebooking://payment-result?bookingId={bookingId}&status={(isSuccess ? "Paid" : "Failed")}";

        var html = $@"<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>Kết quả thanh toán</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            margin: 0;
            background-color: #121212;
            color: #ffffff;
            text-align: center;
        }}
        .card {{
            background: #1e1e1e;
            padding: 32px 24px;
            border-radius: 16px;
            box-shadow: 0 8px 24px rgba(0,0,0,0.5);
            max-width: 420px;
            width: 90%;
            border: 1px solid #333;
        }}
        .icon {{ font-size: 48px; margin-bottom: 16px; }}
        h2 {{ margin: 0 0 12px; font-size: 22px; }}
        p {{ margin: 0 0 24px; color: #a0a0a0; font-size: 15px; line-height: 1.5; }}
        .btn {{
            display: inline-block;
            background-color: #e50914;
            color: white;
            border: none;
            padding: 14px 28px;
            border-radius: 8px;
            font-size: 16px;
            font-weight: 600;
            cursor: pointer;
            text-decoration: none;
            width: 100%;
            box-sizing: border-box;
        }}
    </style>
</head>
<body>
    <div class=""card"">
        <div class=""icon"">{icon}</div>
        <h2>{statusTitle}</h2>
        <p>{statusDesc}</p>
        <button class=""btn"" onclick=""closeWindow()"">Quay lại ứng dụng (Đóng cửa sổ)</button>
    </div>
    <script>
        function closeWindow() {{
            try {{
                // Điều hướng về Movie Booking mobile app qua deep link
                window.location.href = ""{deepLink}"";
            }} catch (e) {{}}
            // Thử đóng cửa sổ nếu là popup trên desktop browser
            try {{
                window.close();
            }} catch (e) {{}}
        }}
        // Tự động chuyển tiếp về app sau 1.5 giây
        setTimeout(closeWindow, 1500);
    </script>
</body>
</html>";

        return Content(html, "text/html", Encoding.UTF8);
    }

    [HttpPost("{id:guid}/refund")]
    [MovieBooking.Infrastructure.Security.HasPermission("Refund")]
    public IActionResult RefundPayment(Guid id) => StatusCode(
        StatusCodes.Status501NotImplemented,
        new { message = "VNPAY refund integration is not configured." });

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
