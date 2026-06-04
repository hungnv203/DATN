using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/users")]
public class UsersController : CrudController<User, UserDto>
{
    public UsersController(ICrudService<User, UserDto> crudService) : base(crudService) { }
}
