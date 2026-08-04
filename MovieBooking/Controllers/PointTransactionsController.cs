using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/point-transactions")]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
public class PointTransactionsController : CrudController<PointTransaction, PointTransactionDto>
{
    public PointTransactionsController(IPointTransactionService crudService) : base(crudService) { }

    public override Task<ActionResult<PointTransactionDto>> Create(PointTransactionDto dto, CancellationToken cancellationToken) =>
        Task.FromResult<ActionResult<PointTransactionDto>>(StatusCode(StatusCodes.Status405MethodNotAllowed));

    public override Task<IActionResult> Update(Guid id, PointTransactionDto dto, CancellationToken cancellationToken) =>
        Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status405MethodNotAllowed));

    public override Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status405MethodNotAllowed));
}
