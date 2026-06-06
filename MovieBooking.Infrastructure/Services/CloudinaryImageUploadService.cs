using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using MovieBooking.Application.Common.Interfaces;

namespace MovieBooking.Infrastructure.Services;

public class CloudinaryImageUploadService : IImageUploadService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryImageUploadService(IConfiguration configuration)
    {
        var cloudName = configuration["Cloudinary:CloudName"];
        var apiKey = configuration["Cloudinary:ApiKey"];
        var apiSecret = configuration["Cloudinary:ApiSecret"];

        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            throw new InvalidOperationException("Cloudinary configuration settings are missing in appsettings.json.");
        }

        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
        _cloudinary.Api.Secure = true;
    }

    public async Task<string> UploadImageAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(fileName, fileStream),
            Folder = "movie_booking_posters",
            UseFilename = true,
            UniqueFilename = true
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams, cancellationToken);

        if (uploadResult.Error != null)
        {
            throw new Exception($"Cloudinary upload failed: {uploadResult.Error.Message}");
        }

        return uploadResult.SecureUrl?.ToString() ?? string.Empty;
    }
}
