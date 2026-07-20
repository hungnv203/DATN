using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/notifications")]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
public class NotificationsController : CrudController<Notification, NotificationDto>
{
    public NotificationsController(ICrudService<Notification, NotificationDto> crudService) : base(crudService) { }
}
