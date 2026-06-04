using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/loyalty-points")]
public class LoyaltyPointsController : CrudController<LoyaltyPoint, LoyaltyPointDto>
{
    public LoyaltyPointsController(ICrudService<LoyaltyPoint, LoyaltyPointDto> crudService) : base(crudService) { }
}
