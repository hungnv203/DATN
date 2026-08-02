using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;
using MovieBooking.Infrastructure.Security;

namespace MovieBooking.Controllers;

[Route("api/tickets")]
public class TicketsController : CrudController<Ticket, TicketDto>
{
    private readonly AppDbContext _db;

    public TicketsController(ITicketService crudService, AppDbContext db) : base(crudService)
    {
        _db = db;
    }

    [HttpPost("checkin")]
    [HasPermission("CheckIn")]
    public async Task<IActionResult> CheckIn(
        [FromBody] CheckInRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.QrCode))
        {
            return BadRequest("QR Code is required");
        }

        var ticket = await _db.Tickets
            .Include(t => t.Booking)
            .ThenInclude(b => b.Showtime)
            .FirstOrDefaultAsync(t => t.QrCode == request.QrCode, cancellationToken);

        if (ticket == null)
        {
            return NotFound(new { Success = false, Message = "Vé không tồn tại hoặc mã QR không hợp lệ." });
        }

        if (ticket.Status == "CheckedIn")
        {
            return BadRequest(new { Success = false, Message = "Vé này đã được sử dụng để check-in trước đó." });
        }

        if (ticket.Status == "Cancelled")
        {
            return BadRequest(new { Success = false, Message = "Vé này đã bị hủy." });
        }

        if (ticket.Booking.Status != "Paid")
        {
            return BadRequest(new { Success = false, Message = "Đơn vé chưa được thanh toán thành công." });
        }

        ticket.Status = "CheckedIn";
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { Success = true, Message = "Check-in thành công!", TicketId = ticket.Id });
    }
}

public class CheckInRequest
{
    public string QrCode { get; set; } = string.Empty;
}
