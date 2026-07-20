using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/seat-holds")]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
public class SeatHoldsController : CrudController<SeatHold, SeatHoldDto>
{
    public SeatHoldsController(ICrudService<SeatHold, SeatHoldDto> crudService) : base(crudService) { }
}
