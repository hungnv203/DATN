using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/booking-promotions")]
public class BookingPromotionsController : CrudController<BookingPromotion, BookingPromotionDto>
{
    public BookingPromotionsController(ICrudService<BookingPromotion, BookingPromotionDto> crudService) : base(crudService) { }
}
