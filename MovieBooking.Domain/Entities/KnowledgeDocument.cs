using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class KnowledgeDocument : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Guid? CinemaId { get; set; }
    public Cinema? Cinema { get; set; }
    public float[] Embedding { get; set; } = [];
}
