using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using MovieBooking.Application.Common.Configuration;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Infrastructure.Services;
using Xunit;

namespace MovieBooking.Tests;

public sealed class GeminiAssistantClientTests
{
    [Fact]
    public async Task CompleteAsync_ValidJson_ParsesSuccessfully()
    {
        var movieId = Guid.NewGuid();
        var geminiResponse = $$"""
        {
            "candidates": [
                {
                    "content": {
                        "parts": [
                            {
                                "text": "{\"kind\":\"GroundedResult\",\"text\":\"Found 1 movie.\",\"language\":\"en\",\"movieIds\":[\"{{movieId}}\"],\"reasons\":[{\"movieId\":\"{{movieId}}\",\"reason\":\"Matches preference.\"}],\"clarificationChoices\":[]}"
                            }
                        ],
                        "role": "model"
                    },
                    "finishReason": "STOP",
                    "index": 0
                }
            ]
        }
        """;

        var client = CreateClient(geminiResponse, HttpStatusCode.OK);
        var result = await client.CompleteAsync(CreateRequest());

        Assert.Equal("GroundedResult", result.Kind);
        Assert.Equal("Found 1 movie.", result.Text);
        Assert.Equal("en", result.Language);
        var id = Assert.Single(result.MovieIds);
        Assert.Equal(movieId, id);
        Assert.Equal("Matches preference.", result.Reasons[movieId]);
    }

    [Fact]
    public async Task CompleteAsync_MarkdownCodeBlockWrapped_ParsesSuccessfully()
    {
        var movieId = Guid.NewGuid();
        var geminiResponse = $$"""
        {
            "candidates": [
                {
                    "content": {
                        "parts": [
                            {
                                "text": "```json\n{\"kind\":\"GroundedResult\",\"text\":\"Found movie.\",\"language\":\"vi\",\"movieIds\":[\"{{movieId}}\"],\"reasons\":[{\"movieId\":\"{{movieId}}\",\"reason\":\"Hay.\"}],\"clarificationChoices\":[]}\n```"
                            }
                        ],
                        "role": "model"
                    },
                    "finishReason": "STOP",
                    "index": 0
                }
            ]
        }
        """;

        var client = CreateClient(geminiResponse, HttpStatusCode.OK);
        var result = await client.CompleteAsync(CreateRequest());

        Assert.Equal("GroundedResult", result.Kind);
        Assert.Equal("Found movie.", result.Text);
        Assert.Equal("vi", result.Language);
        var id = Assert.Single(result.MovieIds);
        Assert.Equal(movieId, id);
    }

    [Fact]
    public async Task CompleteAsync_FinishReasonMaxTokens_ThrowsDescriptiveInvalidDataException()
    {
        var geminiResponse = """
        {
            "candidates": [
                {
                    "content": {
                        "parts": [
                            {
                                "text": "{\"kind\":\"GroundedResult\",\"text\":\"Dưới đây là một số bộ phim hành "
                            }
                        ],
                        "role": "model"
                    },
                    "finishReason": "MAX_TOKENS",
                    "index": 0
                }
            ]
        }
        """;

        var client = CreateClient(geminiResponse, HttpStatusCode.OK);
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => client.CompleteAsync(CreateRequest()));

        Assert.Contains("MAX_TOKENS", exception.Message);
    }

    [Fact]
    public async Task CompleteAsync_FinishReasonSafety_ThrowsDescriptiveInvalidDataException()
    {
        var geminiResponse = """
        {
            "candidates": [
                {
                    "finishReason": "SAFETY",
                    "index": 0
                }
            ]
        }
        """;

        var client = CreateClient(geminiResponse, HttpStatusCode.OK);
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => client.CompleteAsync(CreateRequest()));

        Assert.Contains("SAFETY", exception.Message);
    }

    [Fact]
    public async Task CompleteAsync_WithThoughtParts_FiltersOutThoughtsAndParsesResponse()
    {
        var movieId = Guid.NewGuid();
        var geminiResponse = $$"""
        {
            "candidates": [
                {
                    "content": {
                        "parts": [
                            {
                                "thought": true,
                                "text": "I should recommend this movie based on user preferences."
                            },
                            {
                                "text": "{\"kind\":\"GroundedResult\",\"text\":\"Recommendation.\",\"language\":\"en\",\"movieIds\":[\"{{movieId}}\"],\"reasons\":[{\"movieId\":\"{{movieId}}\",\"reason\":\"Reason.\"}],\"clarificationChoices\":[]}"
                            }
                        ],
                        "role": "model"
                    },
                    "finishReason": "STOP",
                    "index": 0
                }
            ]
        }
        """;

        var client = CreateClient(geminiResponse, HttpStatusCode.OK);
        var result = await client.CompleteAsync(CreateRequest());

        Assert.Equal("GroundedResult", result.Kind);
        Assert.Equal("Recommendation.", result.Text);
        Assert.Single(result.MovieIds);
    }

    [Fact]
    public async Task CompleteAsync_TruncatedMalformedJson_ThrowsInvalidDataException()
    {
        var geminiResponse = """
        {
            "candidates": [
                {
                    "content": {
                        "parts": [
                            {
                                "text": "{\"kind\":\"GroundedResult\",\"text\":\"incomplete"
                            }
                        ],
                        "role": "model"
                    },
                    "finishReason": "STOP",
                    "index": 0
                }
            ]
        }
        """;

        var client = CreateClient(geminiResponse, HttpStatusCode.OK);
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => client.CompleteAsync(CreateRequest()));

        Assert.Contains("malformed structured output", exception.Message);
    }

    private static GeminiAssistantClient CreateClient(string responseBody, HttpStatusCode statusCode)
    {
        var handler = new FakeHttpMessageHandler(responseBody, statusCode);
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new AssistantOptions
        {
            ApiKey = "test-api-key",
            Model = "gemini-3.6-flash",
            MaxOutputTokens = 4096,
            ThinkingBudget = 0
        });

        return new GeminiAssistantClient(httpClient, options);
    }

    private static AiAssistantRequest CreateRequest() => new()
    {
        Message = "Recommend an action movie",
        Locale = "en",
        MaxCards = 5,
        History = [],
        Movies = []
    };

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _response;
        private readonly HttpStatusCode _statusCode;

        public FakeHttpMessageHandler(string response, HttpStatusCode statusCode)
        {
            _response = response;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_response, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
