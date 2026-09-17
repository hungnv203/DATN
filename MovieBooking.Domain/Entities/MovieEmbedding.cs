using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class MovieEmbedding : BaseEntity
{
    public Guid MovieId { get; set; }
    public Movie Movie { get; set; } = null!;
    public string ContentHash { get; set; } = string.Empty;
    public string EmbeddedText { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = [];
    public DateTime LastUpdatedAt { get; set; }
}
