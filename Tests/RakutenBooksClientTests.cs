using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Site.Services;
using Xunit;

namespace Tests;

public class RakutenBooksClientTests
{
    [Fact]
    public async Task SearchByIsbnAsync_IncludesOutOfStockItemsInRequest()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHandler(request =>
        {
            capturedRequest = request;
            return JsonResponse("""
                {
                  "items": [
                    { "title": "羊のうた（5）", "isbn": "9784344800267" }
                  ]
                }
                """);
        });
        var client = CreateClient(handler);

        var result = await client.SearchByIsbnAsync(
            "BooksBook/Search/20170404",
            "9784344800267");

        Assert.True(result.HasValue);
        Assert.Contains("isbn=9784344800267", capturedRequest!.RequestUri!.Query);
        Assert.Contains("outOfStockFlag=1", capturedRequest.RequestUri.Query);
    }

    [Fact]
    public async Task SearchByJanAsync_ReturnsFirstItem_AndSendsRequiredQueryParameters()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHandler(request =>
        {
            capturedRequest = request;
            return JsonResponse("""
                {
                  "items": [
                    { "title": "Game A", "jan": "4902370550733" },
                    { "title": "Game B", "jan": "4902370550740" }
                  ]
                }
                """);
        });
        var client = CreateClient(handler);

        var result = await client.SearchByJanAsync(
            "BooksGame/Search/20170404",
            "4902370550733");

        Assert.True(result.HasValue);
        Assert.Equal("Game A", result.Value.GetProperty("title").GetString());
        Assert.Contains("applicationId=test-app-id", capturedRequest!.RequestUri!.Query);
        Assert.Contains("formatVersion=2", capturedRequest.RequestUri.Query);
        Assert.Contains("jan=4902370550733", capturedRequest.RequestUri.Query);
        Assert.DoesNotContain("outOfStockFlag", capturedRequest.RequestUri.Query);
    }

    [Theory]
    [InlineData("""{ "items": [] }""")]
    [InlineData("""{ "count": 0 }""")]
    public async Task SearchByJanAsync_ReturnsNull_WhenItemsAreEmptyOrMissing(string json)
    {
        var client = CreateClient(new StubHandler(_ => JsonResponse(json)));

        var result = await client.SearchByJanAsync(
            "BooksGame/Search/20170404",
            "4902370550733");

        Assert.Null(result);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SearchByJanAsync_ReturnsNull_WhenHttpRequestFails(HttpStatusCode statusCode)
    {
        var client = CreateClient(new StubHandler(
            _ => new HttpResponseMessage(statusCode)));

        var result = await client.SearchByJanAsync(
            "BooksGame/Search/20170404",
            "4902370550733");

        Assert.Null(result);
    }

    [Fact]
    public async Task SearchByJanAsync_ReturnsNull_WhenApplicationIdIsMissing()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException(
            "HTTP must not be called when the application ID is missing."));
        var client = CreateClient(handler, applicationId: null);

        var result = await client.SearchByJanAsync(
            "BooksGame/Search/20170404",
            "4902370550733");

        Assert.False(client.IsConfigured);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("""[]""")]
    public async Task SearchByJanAsync_ReturnsNull_WhenResponseJsonIsInvalid(string json)
    {
        var client = CreateClient(new StubHandler(_ => JsonResponse(json)));

        var result = await client.SearchByJanAsync(
            "BooksGame/Search/20170404",
            "4902370550733");

        Assert.Null(result);
    }

    private static RakutenBooksClient CreateClient(
        HttpMessageHandler handler,
        string? applicationId = "test-app-id")
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/services/api/")
        };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient("RakutenBooks")).Returns(httpClient);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExternalApis:RakutenApplicationId"] = applicationId
            })
            .Build();

        return new RakutenBooksClient(
            factory.Object,
            configuration,
            NullLogger<RakutenBooksClient>.Instance);
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
