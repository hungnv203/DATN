using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/[controller]")]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
public class RolePermissionsController : CrudController<RolePermission, RolePermissionDto>
{
    public RolePermissionsController(IRolePermissionService crudService) : base(crudService)
    {
    }
}

