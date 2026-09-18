using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Infrastructure.Services.Assistant;

internal static class MovieEmbeddingContentBuilder
{
    public static string Build(Movie movie)
    {
        var genres = movie.MovieGenres
            .Where(item => item.Genre is not null && !string.IsNullOrWhiteSpace(item.Genre.Name))
            .Select(item => item.Genre.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(name => name, StringComparer.Ordinal)
            .ToList();

        var builder = new StringBuilder();
        builder.Append(movie.Title);
        builder.Append(" - ");
        builder.Append(movie.Description);
        builder.Append(" - Duration: ")
            .Append(movie.Duration.ToString(CultureInfo.InvariantCulture))
            .Append(" minutes");
        builder.Append(" - Language: ").Append(movie.Language);
        builder.Append(" - Rating: ").Append(movie.Rating);

        if (genres.Count > 0)
        {
            builder.Append(" - Genres: ").Append(string.Join(", ", genres));
        }

        return builder.ToString();
    }

    public static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }
}
