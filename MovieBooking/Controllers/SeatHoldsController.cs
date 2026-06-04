using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/seat-holds")]
public class SeatHoldsController : CrudController<SeatHold, SeatHoldDto>
{
    public SeatHoldsController(ICrudService<SeatHold, SeatHoldDto> crudService) : base(crudService) { }
}
