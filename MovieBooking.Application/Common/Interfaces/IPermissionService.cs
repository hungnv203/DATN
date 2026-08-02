using MovieBooking.Application.Common.DTOs;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Application.Common.Interfaces;

public interface IPermissionService : ICrudService<Permission, PermissionDto>
{
}

