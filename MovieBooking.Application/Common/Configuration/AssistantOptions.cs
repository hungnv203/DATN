namespace MovieBooking.Application.Common.Configuration;

public sealed class AssistantOptions
{
    public const string SectionName = "AI";
    public bool Enabled { get; set; }
    public string Model { get; set; } = "gemini-3.6-flash";
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxHistoryMessages { get; set; } = 12;
    public int MaxMessageCharacters { get; set; } = 1000;
    public int MaxConversationCharacters { get; set; } = 8000;
    public int MaxMovieCards { get; set; } = 5;
    public int MaxOutputTokens { get; set; } = 4096;
    public int ThinkingBudget { get; set; } = 0;
}
