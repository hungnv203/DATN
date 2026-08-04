using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/payment-logs")]
[Authorize(Roles = "Admin")]
public class PaymentLogsController : CrudController<PaymentLog, PaymentLogDto>
{
    public PaymentLogsController(IPaymentLogService crudService) : base(crudService) { }

    public override Task<ActionResult<PaymentLogDto>> Create(PaymentLogDto dto, CancellationToken cancellationToken) =>
        Task.FromResult<ActionResult<PaymentLogDto>>(StatusCode(StatusCodes.Status405MethodNotAllowed));

    public override Task<IActionResult> Update(Guid id, PaymentLogDto dto, CancellationToken cancellationToken) =>
        Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status405MethodNotAllowed));

    public override Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status405MethodNotAllowed));
}
