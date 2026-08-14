namespace MovieBooking.Application.Common.Configuration;

public sealed class AssistantOptions
{
    public const string SectionName = "AI";

    public bool Enabled { get; init; }
    public string Provider { get; init; } = "OpenAI";
    public string Model { get; init; } = "gpt-4o-mini";
    public string ApiKey { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://api.openai.com/v1/";
    public int TimeoutSeconds { get; init; } = 30;
    public int MaxHistoryMessages { get; init; } = 12;
    public int MaxMessageCharacters { get; init; } = 1000;
    public int MaxConversationCharacters { get; init; } = 8000;
    public int MaxMovieCards { get; init; } = 5;
}
