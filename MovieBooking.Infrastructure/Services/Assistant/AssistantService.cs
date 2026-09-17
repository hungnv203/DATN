using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieBooking.Application.Common.Configuration;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

public sealed class AssistantService : IAssistantService
{
    private readonly IAssistantMovieCatalogue _catalogue;
    private readonly IAiAssistantClient _client;
    private readonly AssistantOptions _options;
    private readonly ILogger<AssistantService> _logger;
    private readonly AppDbContext _db;

    public AssistantService(
        IAssistantMovieCatalogue catalogue,
        IAiAssistantClient client,
        IOptions<AssistantOptions> options,
        ILogger<AssistantService> logger,
        AppDbContext db)
    {
        _catalogue = catalogue;
        _client = client;
        _options = options.Value;
        _logger = logger;
        _db = db;
    }

    public async Task<AssistantResponseDto> SendAsync(SendAssistantMessageRequestDto request, CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var stopwatch = Stopwatch.StartNew();
        if (!_options.Enabled) return Unavailable(correlationId, request.Locale, false);
        Validate(request);

        try
        {
            var semanticMovies = await _catalogue.SearchBySemanticQueryAsync(
                request.Message.Trim(),
                Math.Clamp(_options.MaxMovieCards, 1, 5),
                cancellationToken);

            var knowledgeDocs = await _catalogue.SearchKnowledgeDocumentsAsync(
                request.Message.Trim(),
                3,
                cancellationToken);

            var showtimes = await GetShowtimesForMoviesAsync(
                semanticMovies.Select(m => m.Id).ToList(),
                cancellationToken);

            var contextBuilder = new System.Text.StringBuilder();
            contextBuilder.AppendLine($"CURRENT_TIME_UTC: {DateTime.UtcNow:O}");
            contextBuilder.AppendLine();

            if (semanticMovies.Count > 0)
            {
                contextBuilder.AppendLine("TOP_MATCHING_MOVIES:");
                foreach (var movie in semanticMovies)
                {
                    contextBuilder.AppendLine($"- {movie.Title} ({movie.Rating}, {movie.Duration}min): {movie.Description}");
                }
                contextBuilder.AppendLine();
            }

            if (knowledgeDocs.Count > 0)
            {
                contextBuilder.AppendLine("RELEVANT_POLICIES:");
                foreach (var doc in knowledgeDocs)
                {
                    contextBuilder.AppendLine($"[{doc.Category}] {doc.Title}: {doc.Content}");
                }
                contextBuilder.AppendLine();
            }

            if (showtimes.Count > 0)
            {
                contextBuilder.AppendLine("AVAILABLE_SHOWTIMES:");
                foreach (var showtime in showtimes)
                {
                    contextBuilder.AppendLine($"- Phim: {showtime.MovieTitle} | Rạp: {showtime.CinemaName} | Phòng: {showtime.RoomName} ({showtime.RoomType}) | Suất: {showtime.StartTime:yyyy-MM-dd HH:mm} - {showtime.EndTime:HH:mm} | Giá vé: {showtime.BasePrice:N0} VNĐ");
                }
            }

            var candidateList = semanticMovies.Count > 0 ? semanticMovies : await _catalogue.GetCandidatesAsync(cancellationToken);
            var aiResult = await _client.CompleteAsync(new AiAssistantRequest
            {
                Message = request.Message.Trim(),
                Locale = NormalizeLocale(request.Locale),
                History = request.History,
                Movies = candidateList,
                MaxCards = Math.Clamp(_options.MaxMovieCards, 1, 5),
                Context = contextBuilder.ToString()
            }, cancellationToken);

            var byId = candidateList.ToDictionary(movie => movie.Id);
            var showtimesByMovieId = showtimes.GroupBy(s => s.MovieId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<AssistantShowtimeDto>)g.Take(5).ToList());
            var cards = aiResult.MovieIds.Distinct().Where(byId.ContainsKey)
                .Take(Math.Clamp(_options.MaxMovieCards, 1, 5))
                .Select(id => ToCard(byId[id], aiResult.Reasons.GetValueOrDefault(id) ?? string.Empty,
                    showtimesByMovieId.GetValueOrDefault(id) ?? [])).ToList();

            var kind = NormalizeKind(aiResult.Kind, cards.Count);
            if (kind != AssistantResultKinds.GroundedResult) cards.Clear();
            var choices = kind == AssistantResultKinds.Clarification
                ? aiResult.ClarificationChoices.Where(value => !string.IsNullOrWhiteSpace(value)).Take(5).ToList()
                : [];
            Log(correlationId, kind, aiResult.Language, cards.Count, stopwatch.ElapsedMilliseconds);
            return new AssistantResponseDto
            {
                Kind = kind,
                Text = string.IsNullOrWhiteSpace(aiResult.Text) ? Fallback(request.Locale) : aiResult.Text.Trim(),
                Language = NormalizeLocale(aiResult.Language),
                CorrelationId = correlationId,
                Movies = cards,
                ClarificationChoices = choices
            };
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Timeout connecting to AI provider.");
            return Unavailable(correlationId, request.Locale, true);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed when connecting to AI provider.");
            return Unavailable(correlationId, request.Locale, true);
        }
        catch (InvalidDataException ex)
        {
            _logger.LogError(ex, "Invalid data received from AI provider.");
            return Unavailable(correlationId, request.Locale, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in AssistantService.");
            return Unavailable(correlationId, request.Locale, true);
        }
    }

    private async Task<List<AssistantShowtimeDto>> GetShowtimesForMoviesAsync(
        List<Guid> movieIds,
        CancellationToken cancellationToken)
    {
        if (movieIds.Count == 0) return [];

        var now = DateTime.UtcNow;
        var sevenDaysFromNow = now.AddDays(7);
        return await _db.Showtimes
            .AsNoTracking()
            .Where(s => movieIds.Contains(s.MovieId)
                && s.StartTime >= now.AddHours(-2)
                && s.StartTime <= sevenDaysFromNow
                && s.Status != "Cancelled")
            .Include(s => s.Movie)
            .Include(s => s.Room)
                .ThenInclude(r => r.Cinema)
            .Select(s => new AssistantShowtimeDto
            {
                ShowtimeId = s.Id,
                MovieId = s.MovieId,
                MovieTitle = s.Movie.Title,
                CinemaName = s.Room.Cinema.Name,
                RoomName = s.Room.Name,
                RoomType = s.Room.Type,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                BasePrice = s.BasePrice
            })
            .OrderBy(s => s.StartTime)
            .ToListAsync(cancellationToken);
    }

    private void Validate(SendAssistantMessageRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > _options.MaxMessageCharacters)
            throw new ArgumentException("Message is required and must be within the allowed length.", nameof(request));
        
        var historyCount = request.History?.Count ?? 0;
        var historySum = request.History?.Sum(item => item.Content?.Length ?? 0) ?? 0;
        
        if (historyCount > _options.MaxHistoryMessages
            || (request.History?.Any(item => (item.Content?.Length ?? 0) > _options.MaxMessageCharacters) ?? false)
            || request.Message.Length + historySum > _options.MaxConversationCharacters)
            throw new ArgumentException("Conversation history exceeds the allowed limit.", nameof(request));
    }

    private void Log(string correlationId, string kind, string language, int resultCount, long durationMs) =>
        _logger.LogInformation("Assistant completed. CorrelationId={CorrelationId} Result={Result} Language={Language} ResultCount={ResultCount} DurationMs={DurationMs}", correlationId, kind, NormalizeLocale(language), resultCount, durationMs);

    private static AssistantMovieCardDto ToCard(AssistantMovieCandidateDto movie, string reason, IReadOnlyList<AssistantShowtimeDto> showtimes) => new()
    {
        Id = movie.Id, Title = movie.Title, Description = movie.Description, Duration = movie.Duration,
        ReleaseDate = movie.ReleaseDate, Language = movie.Language, Rating = movie.Rating,
        PosterUrl = movie.PosterUrl, Status = movie.Status, Genres = movie.Genres, Reason = reason,
        UpcomingShowtimes = showtimes
    };

    private static string NormalizeKind(string kind, int count) => kind switch
    {
        AssistantResultKinds.GroundedResult when count > 0 => AssistantResultKinds.GroundedResult,
        AssistantResultKinds.Clarification => AssistantResultKinds.Clarification,
        AssistantResultKinds.Refusal => AssistantResultKinds.Refusal,
        _ => AssistantResultKinds.NoResult
    };

    private static string NormalizeLocale(string locale) => locale.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "vi";
    private static string Fallback(string locale) => NormalizeLocale(locale) == "en" ? "I could not find a grounded answer." : "Mình chưa tìm thấy câu trả lời phù hợp từ dữ liệu phim.";
    private static AssistantResponseDto Unavailable(string correlationId, string locale, bool retryable) => new()
    {
        Kind = AssistantResultKinds.Unavailable,
        Text = NormalizeLocale(locale) == "en" ? "The movie assistant is temporarily unavailable." : "Trợ lý phim hiện tạm thời không khả dụng.",
        Language = NormalizeLocale(locale), CorrelationId = correlationId, Retryable = retryable
    };
}
