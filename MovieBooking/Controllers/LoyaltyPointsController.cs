using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using System.Security.Claims;

namespace MovieBooking.Controllers;

[Route("api/loyalty-points")]
public class LoyaltyPointsController : CrudController<LoyaltyPoint, LoyaltyPointDto>
{
    private readonly ILoyaltyService _loyaltyService;

    public LoyaltyPointsController(
        ILoyaltyPointService crudService,
        ILoyaltyService loyaltyService) : base(crudService)
    {
        _loyaltyService = loyaltyService;
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
    public override Task<ActionResult<IReadOnlyList<LoyaltyPointDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        return base.GetAll(cancellationToken);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
    public override Task<ActionResult<LoyaltyPointDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        return base.GetById(id, cancellationToken);
    }

    [HttpGet("me")]
    public async Task<ActionResult<LoyaltyWalletDto>> GetMyWallet(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
        }

        var wallet = await _loyaltyService.GetWalletAsync(userId, cancellationToken);
        return Ok(wallet);
    }
}

