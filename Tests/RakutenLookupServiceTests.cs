using System.Text.Json;
using Site.Services;
using Xunit;

namespace Tests;

public class RakutenLookupServiceTests
{
    [Fact]
    public void GameMapResult_MapsRakutenFields()
    {
        var item = ParseItem("""
            {
              "title": "Sample Game",
              "hardware": "Nintendo Switch",
              "label": "Sample Publisher",
              "salesDate": "2022年09月01日",
              "largeImageUrl": "https://example.test/game.jpg",
              "jan": "4902370550733"
            }
            """);

        var result = RakutenGameLookupService.MapResult(item);

        Assert.Equal("Sample Game", result.Title);
        Assert.Equal("Nintendo Switch", result.Platform);
        Assert.Equal("Sample Publisher", result.Publisher);
        Assert.Equal(new DateTime(2022, 9, 1), result.ReleaseDate);
        Assert.Equal("https://example.test/game.jpg", result.CoverImageUrl);
        Assert.Equal("4902370550733", result.Jan);
    }

    [Fact]
    public void CdMapResult_MapsRakutenFields()
    {
        var item = ParseItem("""
            {
              "title": "Sample Album",
              "artistName": "Sample Artist",
              "label": "Sample Label",
              "salesDate": "2021年05月01日",
              "largeImageUrl": "https://example.test/cd.jpg",
              "jan": "4547366680049"
            }
            """);

        var result = RakutenCdLookupService.MapResult(item);

        Assert.Equal("Sample Album", result.Title);
        Assert.Equal("Sample Artist", result.Creator);
        Assert.Equal("Sample Label", result.Publisher);
        Assert.Equal(new DateTime(2021, 5, 1), result.ReleaseDate);
        Assert.Equal("https://example.test/cd.jpg", result.CoverImageUrl);
        Assert.Equal("4547366680049", result.Jan);
        Assert.Null(result.DiscCount);
    }

    [Fact]
    public void DvdMapResult_MapsRakutenFields_AndLeavesUnknownFieldsNull()
    {
        var item = ParseItem("""
            {
              "title": "Sample Movie",
              "artistName": "Sample Cast",
              "label": "Sample Studio",
              "salesDate": "2020年11月01日",
              "largeImageUrl": "https://example.test/dvd.jpg",
              "jan": "4988105079149"
            }
            """);

        var result = RakutenDvdLookupService.MapResult(item);

        Assert.Equal("Sample Movie", result.Title);
        Assert.Equal("Sample Cast", result.Creator);
        Assert.Equal("Sample Studio", result.Publisher);
        Assert.Equal(new DateTime(2020, 11, 1), result.ReleaseDate);
        Assert.Equal("https://example.test/dvd.jpg", result.CoverImageUrl);
        Assert.Equal("4988105079149", result.Jan);
        Assert.Null(result.Format);
        Assert.Null(result.DiscCount);
    }

    [Fact]
    public void DvdMapResult_DetectsFormatFromTitle_Bluray()
    {
        var item = ParseItem("""
            {
              "title": "劇場版タイトル [Blu-ray]",
              "jan": "4988105079149"
            }
            """);

        var result = RakutenDvdLookupService.MapResult(item);

        Assert.Equal("Blu-ray", result.Format);
    }

    [Fact]
    public void DvdMapResult_DetectsFormatFromTitle_Dvd()
    {
        var item = ParseItem("""
            {
              "title": "劇場版タイトル [DVD]",
              "jan": "4988105079149"
            }
            """);

        var result = RakutenDvdLookupService.MapResult(item);

        Assert.Equal("DVD", result.Format);
    }

    [Fact]
    public void DvdMapResult_DetectsFormatFromTitle_4KUhd()
    {
        var item = ParseItem("""
            {
              "title": "劇場版タイトル [4K ULTRA HD + Blu-ray]",
              "jan": "4988105079149"
            }
            """);

        var result = RakutenDvdLookupService.MapResult(item);

        Assert.Equal("4K UHD+Blu-ray", result.Format);
    }

    [Theory]
    [InlineData("作品名 Blu-ray BOX", "Blu-ray")]
    [InlineData("作品名 DVD-BOX", "DVD")]
    [InlineData("作品名 ブルーレイ 特別版", "Blu-ray")]
    [InlineData("作品名 BD限定版", "Blu-ray")]
    public void DetectFormat_VariousPatterns(string title, string expected)
    {
        Assert.Equal(expected, RakutenDvdLookupService.DetectFormat(title));
    }

    [Fact]
    public void DetectFormat_ReturnsNull_WhenNoFormatInTitle()
    {
        Assert.Null(RakutenDvdLookupService.DetectFormat("劇場版タイトル"));
        Assert.Null(RakutenDvdLookupService.DetectFormat(null));
        Assert.Null(RakutenDvdLookupService.DetectFormat(""));
    }

    [Theory]
    [InlineData("作品名 [Blu-ray] 2枚組", 2)]
    [InlineData("作品名 3枚セット", 3)]
    [InlineData("作品名 [DVD]", null)]
    public void DetectDiscCount_ParsesFromTitle(string title, int? expected)
    {
        Assert.Equal(expected, RakutenDvdLookupService.DetectDiscCount(title));
    }

    [Fact]
    public void GameMapResult_SetsReleaseDateToNull_WhenSalesDateIsInvalid()
    {
        var item = ParseItem("""{ "salesDate": "2022-09-01" }""");

        var result = RakutenGameLookupService.MapResult(item);

        Assert.Null(result.ReleaseDate);
    }

    private static JsonElement ParseItem(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
