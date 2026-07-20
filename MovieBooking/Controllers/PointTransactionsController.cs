using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/point-transactions")]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
public class PointTransactionsController : CrudController<PointTransaction, PointTransactionDto>
{
    public PointTransactionsController(ICrudService<PointTransaction, PointTransactionDto> crudService) : base(crudService) { }
}
