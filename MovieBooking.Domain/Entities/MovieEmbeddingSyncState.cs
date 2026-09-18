using MovieBooking.Domain.Common;
using MovieBooking.Domain.Constants;

namespace MovieBooking.Domain.Entities;

public class MovieEmbeddingSyncState : BaseEntity
{
    public Guid MovieId { get; set; }
    public Movie Movie { get; set; } = null!;
    public string Status { get; set; } = MovieEmbeddingSyncStatuses.Pending;
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public string RequestedContentHash { get; set; } = string.Empty;
    public string LastError { get; set; } = string.Empty;
}
