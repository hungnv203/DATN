using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Controllers;

[Route("api/tickets")]
public class TicketsController : CrudController<Ticket, TicketDto>
{
    private readonly AppDbContext _db;

    public TicketsController(ICrudService<Ticket, TicketDto> crudService, AppDbContext db) : base(crudService) 
    { 
        _db = db;
    }

    [HttpPost("checkin")]
    public async Task<IActionResult> CheckIn([FromBody] CheckInRequest request)
    {
        if (string.IsNullOrEmpty(request.QrCode))
            return BadRequest("QR Code is required");

        var ticket = await _db.Tickets
            .Include(t => t.Booking)
            .ThenInclude(b => b.Showtime)
            .FirstOrDefaultAsync(t => t.QrCode == request.QrCode);

        if (ticket == null)
            return NotFound(new { Success = false, Message = "Vé không tồn tại hoặc QR không hợp lệ" });

        if (ticket.Status == "CheckedIn")
            return BadRequest(new { Success = false, Message = "Vé này đã được sử dụng (Checked-in) trước đó." });

        if (ticket.Status == "Cancelled")
            return BadRequest(new { Success = false, Message = "Vé này đã bị hủy." });

        if (ticket.Booking.Status != "Paid")
            return BadRequest(new { Success = false, Message = "Đơn vé chưa được thanh toán thành công." });

        // Optionally check Showtime
        // if (ticket.Booking.Showtime.StartTime > DateTime.UtcNow.AddMinutes(30))
        //     return BadRequest(new { Success = false, Message = "Chưa đến giờ check-in (chỉ hỗ trợ check-in trước 30 phút)." });

        ticket.Status = "CheckedIn";
        await _db.SaveChangesAsync();

        return Ok(new { Success = true, Message = "Check-in thành công!", TicketId = ticket.Id });
    }
}

public class CheckInRequest
{
    public string QrCode { get; set; } = string.Empty;
}
