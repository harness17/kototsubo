using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Site.Services;
using Xunit;

namespace Tests;

public class SteamAppDetailsLookupServiceTests
{
    [Fact]
    public void ExtractHeaderImage_SuccessWithHeaderImage_ReturnsUrl()
    {
        using var document = JsonDocument.Parse("""
            {
              "4402650": {
                "success": true,
                "data": {
                  "header_image": "https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/4402650/abc/header.jpg?t=1"
                }
              }
            }
            """);

        var url = SteamAppDetailsLookupService.ExtractHeaderImage(document.RootElement, "4402650");

        Assert.Equal(
            "https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/4402650/abc/header.jpg?t=1",
            url);
    }

    [Fact]
    public void ExtractHeaderImage_SuccessFalse_ReturnsNull()
    {
        using var document = JsonDocument.Parse("""
            { "999999999": { "success": false } }
            """);

        var url = SteamAppDetailsLookupService.ExtractHeaderImage(document.RootElement, "999999999");

        Assert.Null(url);
    }

    [Fact]
    public void ExtractHeaderImage_AppIdNotPresent_ReturnsNull()
    {
        using var document = JsonDocument.Parse("""
            { "10": { "success": true, "data": { "header_image": "https://example.com/x.jpg" } } }
            """);

        var url = SteamAppDetailsLookupService.ExtractHeaderImage(document.RootElement, "20");

        Assert.Null(url);
    }

    [Fact]
    public void ExtractHeaderImage_MissingHeaderImageField_ReturnsNull()
    {
        using var document = JsonDocument.Parse("""
            { "10": { "success": true, "data": { "name": "Counter-Strike" } } }
            """);

        var url = SteamAppDetailsLookupService.ExtractHeaderImage(document.RootElement, "10");

        Assert.Null(url);
    }

    [Fact]
    public void BuildStaticCoverImageCandidates_ValidAppId_PrefersLibraryHero()
    {
        var candidates = SteamAppDetailsLookupService.BuildStaticCoverImageCandidates("3764200");

        Assert.Equal(
            "https://cdn.akamai.steamstatic.com/steam/apps/3764200/library_hero.jpg",
            candidates[0]);
        Assert.Contains(
            "https://cdn.akamai.steamstatic.com/steam/apps/3764200/header.jpg",
            candidates);
    }

    [Fact]
    public void BuildStaticCoverImageCandidates_InvalidAppId_ReturnsEmpty()
    {
        Assert.Empty(SteamAppDetailsLookupService.BuildStaticCoverImageCandidates("bad"));
    }

    [Fact]
    public async Task GetHeaderImageUrlAsync_AppdetailsForbiddenAndLibraryHeroExists_ReturnsLibraryHero()
    {
        var libraryHeroUrl = "https://cdn.akamai.steamstatic.com/steam/apps/3764200/library_hero.jpg";
        var service = CreateService(request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("api/appdetails"))
                return new HttpResponseMessage(HttpStatusCode.Forbidden);

            return url == libraryHeroUrl
                ? ImageResponse()
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var url = await service.GetHeaderImageUrlAsync("3764200");

        Assert.Equal(libraryHeroUrl, url);
    }

    [Fact]
    public async Task GetHeaderImageUrlAsync_AppdetailsForbiddenAndStaticCoversMissing_ReturnsNull()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var url = await service.GetHeaderImageUrlAsync("4402650");

        Assert.Null(url);
    }

    private static SteamAppDetailsLookupService CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var httpClient = new HttpClient(new StubHandler(handler))
        {
            BaseAddress = new Uri("https://store.steampowered.com/")
        };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient("SteamStore")).Returns(httpClient);
        return new SteamAppDetailsLookupService(
            factory.Object,
            NullLogger<SteamAppDetailsLookupService>.Instance);
    }

    private static HttpResponseMessage ImageResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Array.Empty<byte>())
        };
        response.Content.Headers.ContentType = new("image/jpeg");
        return response;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
