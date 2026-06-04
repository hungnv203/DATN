using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/rooms")]
public class RoomsController : CrudController<Room, RoomDto>
{
    public RoomsController(ICrudService<Room, RoomDto> crudService) : base(crudService) { }
}
