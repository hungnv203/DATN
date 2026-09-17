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
    private readonly ITicketService _ticketService;

    public TicketsController(ITicketService crudService) : base(crudService)
    {
        _ticketService = crudService;
    }

    public override Task<ActionResult<TicketDto>> Create(
        TicketDto dto,
        CancellationToken cancellationToken) =>
        Task.FromResult<ActionResult<TicketDto>>(StatusCode(StatusCodes.Status405MethodNotAllowed));

    public override Task<IActionResult> Update(
        Guid id,
        TicketDto dto,
        CancellationToken cancellationToken) =>
        Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status405MethodNotAllowed));

    public override Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status405MethodNotAllowed));

    [HttpPost("checkin")]
    [HasPermission("CheckIn")]
    public async Task<IActionResult> CheckIn(
        [FromBody] CheckInRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _ticketService.CheckInAsync(request.QrCode, cancellationToken);
        
        if (!result.Success)
        {
            if (result.Message.Contains("required") || result.Message.Contains("tồn tại"))
            {
                return NotFound(new { Success = false, Message = result.Message });
            }
            return BadRequest(new { Success = false, Message = result.Message });
        }

        return Ok(new { Success = true, Message = result.Message, TicketId = result.TicketId });
    }
}

public class CheckInRequest
{
    public string QrCode { get; set; } = string.Empty;
}
