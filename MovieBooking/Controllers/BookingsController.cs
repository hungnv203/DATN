using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/bookings")]
public class BookingsController : CrudController<Booking, BookingDto>
{
    public BookingsController(ICrudService<Booking, BookingDto> crudService) : base(crudService) { }
}
