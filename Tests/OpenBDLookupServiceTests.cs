using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Site.Controllers;
using Site.Services;
using Xunit;

namespace Tests;

public class OpenBDLookupServiceTests
{
    [Fact]
    public async Task LookupByIsbnAsync_ValidResponse_MapsSummaryAndPageCount()
    {
        const string json = """
            [{
              "summary": {
                "title": "吾輩は猫である",
                "author": "夏目漱石/著",
                "publisher": "新潮社",
                "pubdate": "20030600",
                "cover": "https://example.test/cover.jpg",
                "isbn": "9784101010014"
              },
              "onix": {
                "DescriptiveDetail": {
                  "Extent": [{ "ExtentValue": "320" }]
                }
              }
            }]
            """;
        var service = CreateService(new StubHandler(HttpStatusCode.OK, json));

        var result = await service.LookupByIsbnAsync("978-4-10-101001-4");

        Assert.NotNull(result);
        Assert.Equal("吾輩は猫である", result.Title);
        Assert.Equal("夏目漱石/著", result.Creator);
        Assert.Equal("新潮社", result.Publisher);
        Assert.Equal(new DateTime(2003, 6, 1), result.ReleaseDate);
        Assert.Equal("https://example.test/cover.jpg", result.CoverImageUrl);
        Assert.Equal("9784101010014", result.ISBN);
        Assert.Equal(320, result.PageCount);
    }

    [Theory]
    [InlineData("2003", 2003, 1, 1)]
    [InlineData("2003-06", 2003, 6, 1)]
    [InlineData("20030615", 2003, 6, 15)]
    public async Task LookupByIsbnAsync_IncompletePublicationDate_UsesAvailablePrecision(
        string publicationDate,
        int year,
        int month,
        int day)
    {
        var json = "[{\"summary\":{\"pubdate\":\"" + publicationDate + "\"}}]";
        var service = CreateService(new StubHandler(HttpStatusCode.OK, json));

        var result = await service.LookupByIsbnAsync("9784101010014");

        Assert.NotNull(result);
        Assert.Equal(new DateTime(year, month, day), result.ReleaseDate);
    }

    [Fact]
    public async Task LookupByIsbnAsync_NotFoundResponse_ReturnsNull()
    {
        var service = CreateService(new StubHandler(HttpStatusCode.OK, "[null]"));

        var result = await service.LookupByIsbnAsync("9784101010014");

        Assert.Null(result);
    }

    [Fact]
    public async Task LookupByIsbnAsync_ApiFailure_ReturnsNull()
    {
        var service = CreateService(new StubHandler(HttpStatusCode.ServiceUnavailable, ""));

        var result = await service.LookupByIsbnAsync("9784101010014");

        Assert.Null(result);
    }

    [Fact]
    public async Task LookupByIsbnAsync_Isbn10_RequestsConvertedIsbn13()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "[null]");
        var service = CreateService(handler);

        await service.LookupByIsbnAsync("4101010013");

        Assert.Contains("isbn=9784101010014", handler.RequestUri?.Query);
    }

    [Fact]
    public async Task LookupByIsbnsAsync_MultipleIsbns_UsesSingleBatchRequestAndKeepsOrder()
    {
        const string json = """
            [
              {"summary":{"title":"Book A","isbn":"9784101010014"}},
              {"summary":{"title":"Book C","isbn":"9784003101018"}}
            ]
            """;
        var handler = new StubHandler(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        var results = await service.LookupByIsbnsAsync(
            ["9784101010014", "9784101010015", "9784003101018"]);

        Assert.Equal("Book A", results[0]?.Title);
        Assert.Null(results[1]);
        Assert.Equal("Book C", results[2]?.Title);
        Assert.Contains("isbn=9784101010014,9784003101018", handler.RequestUri?.Query);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task LookupByIsbnsAsync_MoreThanBatchSize_SplitsRequests()
    {
        var handler = new StubHandler(
            HttpStatusCode.OK,
            "[{\"summary\":{\"title\":\"Book\",\"isbn\":\"9784101010014\"}}]");
        var service = CreateService(handler);
        var isbns = Enumerable
            .Repeat("9784101010014", OpenBDLookupService.BatchSize * 2 + 1)
            .ToList();

        var results = await service.LookupByIsbnsAsync(isbns);

        Assert.Equal(isbns.Count, results.Count);
        Assert.Equal(3, handler.RequestCount);
    }

    [Fact]
    public async Task LookupByIsbnsAsync_MaxImportCount_UsesExpectedBatchCount()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "[]");
        var service = CreateService(handler);
        var isbns = Enumerable
            .Repeat("9784101010014", ImportController.MaxIsbnCount)
            .ToList();

        await service.LookupByIsbnsAsync(isbns);

        Assert.Equal(
            ImportController.MaxIsbnCount / OpenBDLookupService.BatchSize,
            handler.RequestCount);
    }

    [Theory]
    [InlineData("9784101010015")]
    [InlineData("4101010010")]
    [InlineData("not-an-isbn")]
    public async Task LookupByIsbnAsync_InvalidCheckDigit_DoesNotCallApi(string isbn)
    {
        var handler = new StubHandler(HttpStatusCode.OK, "[null]");
        var service = CreateService(handler);

        var result = await service.LookupByIsbnAsync(isbn);

        Assert.Null(result);
        Assert.Equal(0, handler.RequestCount);
    }

    private static OpenBDLookupService CreateService(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExternalApis:OpenBDBaseUrl"] = "https://example.test/openbd"
            })
            .Build();

        return new OpenBDLookupService(
            factory.Object,
            NullLogger<OpenBDLookupService>.Instance,
            configuration);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;
        private int _requestCount;

        public StubHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        public Uri? RequestUri { get; private set; }
        public int RequestCount => _requestCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Interlocked.Increment(ref _requestCount);
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            });
        }
    }
}
