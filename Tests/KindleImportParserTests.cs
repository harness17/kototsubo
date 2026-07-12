using System.Text;
using Site.Services;
using Xunit;

namespace Tests;

public class KindleImportParserTests
{
    private readonly KindleImportParser _parser = new();

    [Fact]
    public void Parse_CsvWithQuotedCommaNewlineAndQuote_MapsFields()
    {
        const string csv = "\uFEFF\"title\",\"authors\",\"asin\",\"series\",\"volume\",\"acquiredTime\",\"readStatus\"\r\n" +
                           "\"Title, Part \"\"One\"\"\r\nContinued\",\"Author A / Author B\",\"B012345678\",\"Series A\",\"2\",\"2026/06/11 22:30\",\"read\"\r\n";
        using var stream = ToStream(csv);

        var rows = _parser.Parse(stream, ".csv");

        var row = Assert.Single(rows);
        Assert.Equal("Title, Part \"One\"\r\nContinued", row.Title);
        Assert.Equal("Author A / Author B", row.Creator);
        Assert.Equal("B012345678", row.ASIN);
        Assert.Equal("Series A", row.Series);
        Assert.Equal(2, row.Volume);
        Assert.Equal(new DateTime(2026, 6, 11, 22, 30, 0), row.AcquiredTime);
        Assert.Equal("read", row.ReadStatus);
    }

    [Fact]
    public void Parse_JsonRootItems_MapsAuthorsUnixTimeAndThumbnail()
    {
        const string json = """
            {
              "scannedAt": 1718100000000,
              "items": [{
                "title": "Book",
                "authors": ["Author A", "Author B"],
                "asin": "B012345678",
                "seriesKey": "Series",
                "volume": 3,
                "acquiredTime": 1718000000000,
                "readStatus": "read",
                "thumbnailUrl": "https://example.test/book.jpg"
              }],
              "series": []
            }
            """;
        using var stream = ToStream(json);

        var rows = _parser.Parse(stream, ".json");

        var row = Assert.Single(rows);
        Assert.Equal("Author A / Author B", row.Creator);
        Assert.Equal("Series", row.Series);
        Assert.Equal(3, row.Volume);
        Assert.NotNull(row.AcquiredTime);
        Assert.Equal("https://example.test/book.jpg", row.ThumbnailUrl);
    }

    [Fact]
    public void Parse_CsvMissingRequiredColumn_ThrowsValidationMessage()
    {
        using var stream = ToStream("\"title\",\"authors\"\r\n\"Book\",\"Author\"\r\n");

        var exception = Assert.Throws<KindleImportException>(
            () => _parser.Parse(stream, ".csv"));

        Assert.Contains("asin", exception.Message);
    }

    [Fact]
    public void Parse_EmptyCsv_ThrowsEmptyFileMessage()
    {
        using var stream = ToStream("");

        var exception = Assert.Throws<KindleImportException>(
            () => _parser.Parse(stream, ".csv"));

        Assert.Equal("ファイルが空です。", exception.Message);
    }

    [Fact]
    public void Parse_UnsupportedExtension_ThrowsValidationMessage()
    {
        using var stream = ToStream("content");

        var exception = Assert.Throws<KindleImportException>(
            () => _parser.Parse(stream, ".txt"));

        Assert.Contains("CSV または JSON", exception.Message);
    }

    [Fact]
    public void Parse_JsonExceedingMaximumItemCount_ThrowsValidationMessage()
    {
        var items = Enumerable.Range(0, KindleImportParser.MaxItemCount + 1)
            .Select(index => $$"""{"title":"Book {{index}}","asin":"{{index:D10}}"}""");
        using var stream = ToStream($$"""{"items":[{{string.Join(",", items)}}]}""");

        var exception = Assert.Throws<KindleImportException>(
            () => _parser.Parse(stream, ".json"));

        Assert.Contains($"{KindleImportParser.MaxItemCount:N0}件まで", exception.Message);
    }

    private static MemoryStream ToStream(string value)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(value));
    }
}
