using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/[controller]")]
public class PermissionsController : CrudController<Permission, PermissionDto>
{
    public PermissionsController(ICrudService<Permission, PermissionDto> crudService) : base(crudService)
    {
    }
}
