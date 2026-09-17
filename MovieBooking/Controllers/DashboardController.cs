using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.Interfaces;

namespace MovieBooking.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        if (days <= 0 || days > 365) days = 30;

        var result = await _dashboardService.GetSummaryAsync(days, cancellationToken);
        return Ok(result);
    }
}
