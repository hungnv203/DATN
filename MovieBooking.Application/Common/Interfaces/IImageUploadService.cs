namespace MovieBooking.Application.Common.Interfaces;

public interface IImageUploadService
{
    Task<string> UploadImageAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
}
