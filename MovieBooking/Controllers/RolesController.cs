using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/roles")]
public class RolesController : CrudController<Role, RoleDto>
{
    public RolesController(ICrudService<Role, RoleDto> crudService) : base(crudService) { }
}
