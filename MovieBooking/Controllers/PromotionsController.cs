using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/promotions")]
public class PromotionsController : CrudController<Promotion, PromotionDto>
{
    public PromotionsController(ICrudService<Promotion, PromotionDto> crudService) : base(crudService) { }
}
