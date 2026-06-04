using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/user-roles")]
public class UserRolesController : CrudController<UserRole, UserRoleDto>
{
    public UserRolesController(ICrudService<UserRole, UserRoleDto> crudService) : base(crudService) { }
}
