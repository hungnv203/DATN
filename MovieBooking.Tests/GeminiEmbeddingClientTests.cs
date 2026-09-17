using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using MovieBooking.Application.Common.Configuration;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Infrastructure.Services.Assistant;
using Xunit;

namespace MovieBooking.Tests;

public sealed class GeminiEmbeddingClientTests
{
    [Fact]
    public async Task EmbedAsync_ValidResponse_ReturnsEmbedding()
    {
        var embeddingValues = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };
        var response = $$"""
        {
            "embedding": {
                "values": [{{string.Join(",", embeddingValues)}}]
            }
        }
        """;

        var client = CreateClient(response, HttpStatusCode.OK);
        var result = await client.EmbedAsync("test text");

        Assert.Equal(embeddingValues.Length, result.Length);
        for (int i = 0; i < embeddingValues.Length; i++)
        {
            Assert.Equal(embeddingValues[i], result[i], 4);
        }
    }

    [Fact]
    public async Task EmbedAsync_EmptyText_ReturnsEmptyArray()
    {
        var client = CreateClient("{}", HttpStatusCode.OK);
        var result = await client.EmbedAsync("");
        Assert.Empty(result);
    }

    [Fact]
    public async Task EmbedAsync_ApiError_ThrowsHttpRequestException()
    {
        var client = CreateClient("error", HttpStatusCode.InternalServerError);
        await Assert.ThrowsAsync<HttpRequestException>(() => client.EmbedAsync("test"));
    }

    [Fact]
    public async Task BatchEmbedAsync_ValidResponse_ReturnsMultipleEmbeddings()
    {
        var response = """
        {
            "embeddings": [
                { "values": [0.1, 0.2, 0.3] },
                { "values": [0.4, 0.5, 0.6] }
            ]
        }
        """;

        var client = CreateClient(response, HttpStatusCode.OK);
        var result = await client.BatchEmbedAsync(["text1", "text2"]);

        Assert.Equal(2, result.Count);
        Assert.Equal(3, result[0].Length);
        Assert.Equal(0.1f, result[0][0], 4);
        Assert.Equal(3, result[1].Length);
        Assert.Equal(0.4f, result[1][0], 4);
    }

    [Fact]
    public async Task BatchEmbedAsync_EmptyList_ReturnsEmptyArray()
    {
        var client = CreateClient("{}", HttpStatusCode.OK);
        var result = await client.BatchEmbedAsync([]);
        Assert.Empty(result);
    }

    private static GeminiEmbeddingClient CreateClient(string responseBody, HttpStatusCode statusCode)
    {
        var handler = new FakeHttpMessageHandler(responseBody, statusCode);
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new AssistantOptions
        {
            ApiKey = "test-api-key"
        });

        return new GeminiEmbeddingClient(httpClient, options);
    }

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
