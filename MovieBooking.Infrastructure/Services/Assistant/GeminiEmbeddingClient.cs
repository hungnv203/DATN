using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MovieBooking.Application.Common.Configuration;
using MovieBooking.Application.Common.Interfaces;

namespace MovieBooking.Infrastructure.Services.Assistant;

public sealed class GeminiEmbeddingClient : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly AssistantOptions _options;

    public GeminiEmbeddingClient(HttpClient httpClient, IOptions<AssistantOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var apiKey = _options.ApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidDataException("Gemini API key is not configured.");

        var requestUri = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:embedContent?key={apiKey}";

        var payload = new
        {
            model = "models/gemini-embedding-001",
            content = new
            {
                parts = new[] { new { text } }
            }
        };

        var response = await _httpClient.PostAsJsonAsync(requestUri, payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        return ExtractEmbedding(document.RootElement);
    }

    public async Task<IReadOnlyList<float[]>> BatchEmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0) return [];

        var apiKey = _options.ApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidDataException("Gemini API key is not configured.");

        var requestUri = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:batchEmbedContents?key={apiKey}";

        var payload = new
        {
            requests = texts.Select(text => new
            {
                model = "models/gemini-embedding-001",
                content = new
                {
                    parts = new[] { new { text } }
                }
            }).ToArray()
        };

        var response = await _httpClient.PostAsJsonAsync(requestUri, payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var embeddings = new List<float[]>();
        if (document.RootElement.TryGetProperty("embeddings", out var embeddingsArray))
        {
            foreach (var embeddingElement in embeddingsArray.EnumerateArray())
            {
                embeddings.Add(ExtractEmbedding(embeddingElement));
            }
        }

        return embeddings;
    }

    private static float[] ExtractEmbedding(JsonElement element)
    {
        if (element.TryGetProperty("embedding", out var embeddingProperty)
            && embeddingProperty.TryGetProperty("values", out var valuesArray)
            && valuesArray.ValueKind == JsonValueKind.Array)
        {
            var values = new float[valuesArray.GetArrayLength()];
            var index = 0;
            foreach (var value in valuesArray.EnumerateArray())
            {
                values[index++] = value.GetSingle();
            }
            return values;
        }

        if (element.TryGetProperty("values", out var directValuesArray)
            && directValuesArray.ValueKind == JsonValueKind.Array)
        {
            var values = new float[directValuesArray.GetArrayLength()];
            var index = 0;
            foreach (var value in directValuesArray.EnumerateArray())
            {
                values[index++] = value.GetSingle();
            }
            return values;
        }

        return [];
    }
}
