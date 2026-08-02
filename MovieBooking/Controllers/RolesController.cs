using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/roles")]
[Authorize(Roles = "Admin")]
public class RolesController : CrudController<Role, RoleDto>
{
    public RolesController(IRoleService crudService) : base(crudService) { }
}

