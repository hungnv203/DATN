namespace MovieBooking.Application.Common.Configuration;

public sealed class AssistantOptions
{
    public const string SectionName = "AI";
    public bool Enabled { get; set; }
    public string Model { get; set; } = "gemini-2.5-flash";
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxHistoryMessages { get; set; } = 12;
    public int MaxMessageCharacters { get; set; } = 1000;
    public int MaxConversationCharacters { get; set; } = 8000;
    public int MaxMovieCards { get; set; } = 5;
}
