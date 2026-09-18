namespace MovieBooking.Application.Common.Configuration;

public sealed class MovieEmbeddingRetryOptions
{
    public const string SectionName = "MovieEmbeddingRetry";

    public int MaxAttempts { get; set; } = 5;
    public int BaseDelaySeconds { get; set; } = 30;
    public int MaxDelaySeconds { get; set; } = 1800;
    public int PollIntervalSeconds { get; set; } = 15;
    public int BatchSize { get; set; } = 10;
}
