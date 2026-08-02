using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : CrudController<User, UserDto>
{
    public UsersController(IUserManagementService crudService) : base(crudService) { }
}

