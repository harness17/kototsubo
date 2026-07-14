using System.Text.Json;
using Site.Services;
using Xunit;

namespace Tests;

public class RakutenBookLookupServiceTests
{
    [Fact]
    public void MapResult_MapsAllFields()
    {
        var item = ParseItem("""
            {
              "title": "プログラミング入門",
              "author": "山田太郎",
              "publisherName": "技術出版社",
              "salesDate": "2024年01月15日",
              "largeImageUrl": "https://example.test/book.jpg",
              "isbn": "9784065342176"
            }
            """);

        var result = RakutenBookLookupService.MapResult(item);

        Assert.Equal("プログラミング入門", result.Title);
        Assert.Equal("山田太郎", result.Creator);
        Assert.Equal("技術出版社", result.Publisher);
        Assert.Equal(new DateTime(2024, 1, 15), result.ReleaseDate);
        Assert.Equal("https://example.test/book.jpg", result.CoverImageUrl);
        Assert.Equal("9784065342176", result.ISBN);
        Assert.Null(result.PageCount);
    }

    [Fact]
    public void MapResult_ParsesSalesDate()
    {
        var item = ParseItem("""{ "salesDate": "2024年01月15日" }""");

        var result = RakutenBookLookupService.MapResult(item);

        Assert.Equal(new DateTime(2024, 1, 15), result.ReleaseDate);
    }

    [Fact]
    public void MapResult_NormalizesIsbn()
    {
        var item = ParseItem("""{ "isbn": "978-4-06-534217-6" }""");

        var result = RakutenBookLookupService.MapResult(item);

        Assert.Equal("9784065342176", result.ISBN);
    }

    [Fact]
    public void MapResult_MapsCoverImageUrl()
    {
        var item = ParseItem("""
            { "largeImageUrl": "https://example.test/cover.jpg" }
            """);

        var result = RakutenBookLookupService.MapResult(item);

        Assert.Equal("https://example.test/cover.jpg", result.CoverImageUrl);
    }

    [Fact]
    public void MapResult_PageCountIsAlwaysNull()
    {
        var item = ParseItem("""
            {
              "title": "テスト書籍",
              "author": "著者名"
            }
            """);

        var result = RakutenBookLookupService.MapResult(item);

        Assert.Null(result.PageCount);
    }

    [Fact]
    public void MapResult_HandlesIncompleteJson_TitleOnly()
    {
        var item = ParseItem("""{ "title": "タイトルのみ" }""");

        var result = RakutenBookLookupService.MapResult(item);

        Assert.Equal("タイトルのみ", result.Title);
        Assert.Null(result.Creator);
        Assert.Null(result.Publisher);
        Assert.Null(result.ReleaseDate);
        Assert.Null(result.CoverImageUrl);
        Assert.Null(result.ISBN);
        Assert.Null(result.PageCount);
    }

    private static JsonElement ParseItem(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
