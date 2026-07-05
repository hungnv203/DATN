using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Infrastructure.Security;

namespace MovieBooking.Controllers;

[ApiController]
[Route("api/upload")]
[HasPermission("Upload")]
public class UploadController : ControllerBase
{
    private readonly IImageUploadService _imageUploadService;
    private readonly string[] _allowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

    public UploadController(IImageUploadService imageUploadService)
    {
        _imageUploadService = imageUploadService;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded." });
        }

        if (file.Length > MaxFileSize)
        {
            return BadRequest(new { message = "File size exceeds the 5MB limit." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_allowedExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Invalid file type. Only JPG, JPEG, PNG, and WEBP are allowed." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            var fileName = $"{Guid.NewGuid()}{extension}";
            var imageUrl = await _imageUploadService.UploadImageAsync(stream, fileName, cancellationToken);

            return Ok(new { url = imageUrl });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
