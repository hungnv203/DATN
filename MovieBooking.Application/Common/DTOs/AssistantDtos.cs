namespace MovieBooking.Application.Common.DTOs;

public static class AssistantResultKinds
{
    public const string GroundedResult = "GroundedResult";
    public const string Clarification = "Clarification";
    public const string NoResult = "NoResult";
    public const string Refusal = "Refusal";
    public const string Unavailable = "Unavailable";
}

public sealed class AssistantMessageDto
{
    public string Role { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}

public sealed class SendAssistantMessageRequestDto
{
    public string Message { get; init; } = string.Empty;
    public string Locale { get; init; } = "vi";
    public IReadOnlyList<AssistantMessageDto> History { get; init; } = [];
}

public sealed class AssistantMovieCardDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int Duration { get; init; }
    public DateTime ReleaseDate { get; init; }
    public string Language { get; init; } = string.Empty;
    public string Rating { get; init; } = string.Empty;
    public string PosterUrl { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<string> Genres { get; init; } = [];
    public string Reason { get; init; } = string.Empty;
}

public sealed class AssistantResponseDto
{
    public string Kind { get; init; } = AssistantResultKinds.Unavailable;
    public string Text { get; init; } = string.Empty;
    public string Language { get; init; } = "vi";
    public string CorrelationId { get; init; } = string.Empty;
    public bool Retryable { get; init; }
    public int? RetryAfterSeconds { get; init; }
    public IReadOnlyList<AssistantMovieCardDto> Movies { get; init; } = [];
    public IReadOnlyList<string> ClarificationChoices { get; init; } = [];
}

public sealed class AssistantMovieCandidateDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int Duration { get; init; }
    public DateTime ReleaseDate { get; init; }
    public string Language { get; init; } = string.Empty;
    public string Rating { get; init; } = string.Empty;
    public string PosterUrl { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<string> Genres { get; init; } = [];
}

public sealed class AiAssistantRequest
{
    public string Message { get; init; } = string.Empty;
    public string Locale { get; init; } = "vi";
    public IReadOnlyList<AssistantMessageDto> History { get; init; } = [];
    public IReadOnlyList<AssistantMovieCandidateDto> Movies { get; init; } = [];
    public int MaxCards { get; init; } = 5;
}

public sealed class AiAssistantResult
{
    public string Kind { get; init; } = AssistantResultKinds.NoResult;
    public string Text { get; init; } = string.Empty;
    public string Language { get; init; } = "vi";
    public IReadOnlyList<Guid> MovieIds { get; init; } = [];
    public IReadOnlyDictionary<Guid, string> Reasons { get; init; } = new Dictionary<Guid, string>();
    public IReadOnlyList<string> ClarificationChoices { get; init; } = [];
}
