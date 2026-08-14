using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieBooking.Application.Common.Configuration;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;

namespace MovieBooking.Infrastructure.Services;

public sealed class AssistantService : IAssistantService
{
    private readonly IAssistantMovieCatalogue _catalogue;
    private readonly IAiAssistantClient _client;
    private readonly AssistantOptions _options;
    private readonly ILogger<AssistantService> _logger;

    public AssistantService(
        IAssistantMovieCatalogue catalogue,
        IAiAssistantClient client,
        IOptions<AssistantOptions> options,
        ILogger<AssistantService> logger)
    {
        _catalogue = catalogue;
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AssistantResponseDto> SendAsync(
        SendAssistantMessageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var stopwatch = Stopwatch.StartNew();

        if (!_options.Enabled)
        {
            return Unavailable(correlationId, request.Locale, false);
        }

        Validate(request);

        try
        {
            var candidates = await _catalogue.GetCandidatesAsync(cancellationToken);
            var aiResult = await _client.CompleteAsync(
                new AiAssistantRequest
                {
                    Message = request.Message.Trim(),
                    Locale = NormalizeLocale(request.Locale),
                    History = request.History,
                    Movies = candidates,
                    MaxCards = Math.Clamp(_options.MaxMovieCards, 1, 5)
                },
                cancellationToken);

            var candidatesById = candidates.ToDictionary(movie => movie.Id);
            var selectedMovies = aiResult.MovieIds
                .Distinct()
                .Where(candidatesById.ContainsKey)
                .Take(Math.Clamp(_options.MaxMovieCards, 1, 5))
                .Select(movieId => ToCard(
                    candidatesById[movieId],
                    aiResult.Reasons.GetValueOrDefault(movieId) ?? string.Empty))
                .ToList();

            var kind = NormalizeKind(aiResult.Kind, selectedMovies.Count);
            if (kind != AssistantResultKinds.GroundedResult)
            {
                selectedMovies.Clear();
            }

            var choices = kind == AssistantResultKinds.Clarification
                ? aiResult.ClarificationChoices.Take(5).ToList()
                : [];

            LogOutcome(correlationId, kind, aiResult.Language, selectedMovies.Count, stopwatch.ElapsedMilliseconds);
            return new AssistantResponseDto
            {
                Kind = kind,
                Text = string.IsNullOrWhiteSpace(aiResult.Text)
                    ? LocalizedFallback(request.Locale)
                    : aiResult.Text.Trim(),
                Language = NormalizeLocale(aiResult.Language),
                CorrelationId = correlationId,
                Movies = selectedMovies,
                ClarificationChoices = choices
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogOutcome(correlationId, AssistantResultKinds.Unavailable, request.Locale, 0, stopwatch.ElapsedMilliseconds);
            return Unavailable(correlationId, request.Locale, true);
        }
        catch (HttpRequestException)
        {
            LogOutcome(correlationId, AssistantResultKinds.Unavailable, request.Locale, 0, stopwatch.ElapsedMilliseconds);
            return Unavailable(correlationId, request.Locale, true);
        }
        catch (InvalidDataException)
        {
            LogOutcome(correlationId, AssistantResultKinds.Unavailable, request.Locale, 0, stopwatch.ElapsedMilliseconds);
            return Unavailable(correlationId, request.Locale, true);
        }
    }

    private void Validate(SendAssistantMessageRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("Message is required.", nameof(request));
        }

        if (request.Message.Length > _options.MaxMessageCharacters)
        {
            throw new ArgumentException("Message is too long.", nameof(request));
        }

        if (request.History.Count > _options.MaxHistoryMessages
            || request.History.Any(item => item.Content.Length > _options.MaxMessageCharacters)
            || request.Message.Length + request.History.Sum(item => item.Content.Length) > _options.MaxConversationCharacters)
        {
            throw new ArgumentException("Conversation history exceeds the allowed limit.", nameof(request));
        }
    }

    private void LogOutcome(string correlationId, string kind, string language, int resultCount, long durationMs)
    {
        _logger.LogInformation(
            "Assistant request completed. CorrelationId={CorrelationId} Result={Result} Language={Language} ResultCount={ResultCount} DurationMs={DurationMs}",
            correlationId,
            kind,
            NormalizeLocale(language),
            resultCount,
            durationMs);
    }

    private static AssistantMovieCardDto ToCard(AssistantMovieCandidateDto movie, string reason)
    {
        return new AssistantMovieCardDto
        {
            Id = movie.Id,
            Title = movie.Title,
            Description = movie.Description,
            Duration = movie.Duration,
            ReleaseDate = movie.ReleaseDate,
            Language = movie.Language,
            Rating = movie.Rating,
            PosterUrl = movie.PosterUrl,
            Status = movie.Status,
            Genres = movie.Genres,
            Reason = reason
        };
    }

    private static string NormalizeKind(string kind, int movieCount)
    {
        return kind switch
        {
            AssistantResultKinds.GroundedResult when movieCount > 0 => AssistantResultKinds.GroundedResult,
            AssistantResultKinds.Clarification => AssistantResultKinds.Clarification,
            AssistantResultKinds.Refusal => AssistantResultKinds.Refusal,
            _ => AssistantResultKinds.NoResult
        };
    }

    private static string NormalizeLocale(string locale)
    {
        return locale.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "vi";
    }

    private static string LocalizedFallback(string locale)
    {
        return NormalizeLocale(locale) == "en"
            ? "I could not find a grounded answer for that request."
            : "Mình chưa tìm thấy câu trả lời phù hợp từ dữ liệu phim hiện có.";
    }

    private static AssistantResponseDto Unavailable(string correlationId, string locale, bool retryable)
    {
        var language = NormalizeLocale(locale);
        return new AssistantResponseDto
        {
            Kind = AssistantResultKinds.Unavailable,
            Text = language == "en"
                ? "The movie assistant is temporarily unavailable."
                : "Trợ lý phim hiện tạm thời không khả dụng.",
            Language = language,
            CorrelationId = correlationId,
            Retryable = retryable
        };
    }
}
