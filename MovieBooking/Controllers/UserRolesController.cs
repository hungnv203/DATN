using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/user-roles")]
[Authorize(Roles = "Admin")]
public class UserRolesController : CrudController<UserRole, UserRoleDto>
{
    public UserRolesController(IUserRoleService crudService) : base(crudService) { }
}
