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
            return BadRequest("QR Code is required");

        var ticket = await _db.Tickets
            .Include(t => t.Booking)
            .ThenInclude(b => b.Showtime)
            .FirstOrDefaultAsync(t => t.QrCode == request.QrCode, cancellationToken);

        if (ticket == null)
            return NotFound(new { Success = false, Message = "VÃƒÆ’Ã‚Â© khÃƒÆ’Ã‚Â´ng tÃƒÂ¡Ã‚Â»Ã¢â‚¬Å“n tÃƒÂ¡Ã‚ÂºÃ‚Â¡i hoÃƒÂ¡Ã‚ÂºÃ‚Â·c QR khÃƒÆ’Ã‚Â´ng hÃƒÂ¡Ã‚Â»Ã‚Â£p lÃƒÂ¡Ã‚Â»Ã¢â‚¬Â¡" });

        if (ticket.Status == "CheckedIn")
            return BadRequest(new { Success = false, Message = "VÃƒÆ’Ã‚Â© nÃƒÆ’Ã‚Â y Ãƒâ€žÃ¢â‚¬ËœÃƒÆ’Ã‚Â£ Ãƒâ€žÃ¢â‚¬ËœÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã‚Â£c sÃƒÂ¡Ã‚Â»Ã‚Â­ dÃƒÂ¡Ã‚Â»Ã‚Â¥ng (Checked-in) trÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã¢â‚¬Âºc Ãƒâ€žÃ¢â‚¬ËœÃƒÆ’Ã‚Â³." });

        if (ticket.Status == "Cancelled")
            return BadRequest(new { Success = false, Message = "VÃƒÆ’Ã‚Â© nÃƒÆ’Ã‚Â y Ãƒâ€žÃ¢â‚¬ËœÃƒÆ’Ã‚Â£ bÃƒÂ¡Ã‚Â»Ã¢â‚¬Â¹ hÃƒÂ¡Ã‚Â»Ã‚Â§y." });

        if (ticket.Booking.Status != "Paid")
            return BadRequest(new { Success = false, Message = "Ãƒâ€žÃ‚ÂÃƒâ€ Ã‚Â¡n vÃƒÆ’Ã‚Â© chÃƒâ€ Ã‚Â°a Ãƒâ€žÃ¢â‚¬ËœÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã‚Â£c thanh toÃƒÆ’Ã‚Â¡n thÃƒÆ’Ã‚Â nh cÃƒÆ’Ã‚Â´ng." });

        // Optionally check Showtime
        // if (ticket.Booking.Showtime.StartTime > DateTime.UtcNow.AddMinutes(30))
        //     return BadRequest(new { Success = false, Message = "ChÃƒâ€ Ã‚Â°a Ãƒâ€žÃ¢â‚¬ËœÃƒÂ¡Ã‚ÂºÃ‚Â¿n giÃƒÂ¡Ã‚Â»Ã‚Â check-in (chÃƒÂ¡Ã‚Â»Ã¢â‚¬Â° hÃƒÂ¡Ã‚Â»Ã¢â‚¬â€ trÃƒÂ¡Ã‚Â»Ã‚Â£ check-in trÃƒâ€ Ã‚Â°ÃƒÂ¡Ã‚Â»Ã¢â‚¬Âºc 30 phÃƒÆ’Ã‚Âºt)." });

        ticket.Status = "CheckedIn";
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { Success = true, Message = "Check-in thÃƒÆ’Ã‚Â nh cÃƒÆ’Ã‚Â´ng!", TicketId = ticket.Id });
    }
}

public class CheckInRequest
{
    public string QrCode { get; set; } = string.Empty;
}

