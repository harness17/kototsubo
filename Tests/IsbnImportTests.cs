using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Site.Common;
using Site.Controllers;
using Site.Entity;
using Site.Models;
using Site.Repository;
using Site.Services;
using System.Text;
using Xunit;

namespace Tests;

public class IsbnImportTests
{
    [Fact]
    public void IsbnImportViewModel_Isbns_IsNotRequiredWhenFileIsUsed()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllersWithViews();
        using var provider = services.BuildServiceProvider();
        var metadataProvider = provider.GetRequiredService<IModelMetadataProvider>();

        var metadata = metadataProvider.GetMetadataForProperty(
            typeof(IsbnImportViewModel),
            nameof(IsbnImportViewModel.Isbns));

        Assert.False(metadata.IsRequired);
    }

    [Fact]
    public void SplitIsbns_MixedSeparators_ReturnsAllValuesInInputOrder()
    {
        var result = ImportController.SplitIsbns(
            "9784101010014, 9784003101018\r\n4101010013\t9784101010014");

        Assert.Equal(
            ["9784101010014", "9784003101018", "4101010013", "9784101010014"],
            result);
    }

    [Fact]
    public void SplitIsbns_FullWidthSeparators_SplitsCorrectly()
    {
        var result = ImportController.SplitIsbns(
            "9784101010014，9784003101018　4101010013");

        Assert.Equal(
            ["9784101010014", "9784003101018", "4101010013"],
            result);
    }

    [Fact]
    public void SplitIsbns_EmptyInput_ReturnsEmptyList()
    {
        Assert.Empty(ImportController.SplitIsbns(" \r\n\t"));
    }

    [Fact]
    public void ToEntity_IsbnImport_UsesRegistrationDate()
    {
        var entity = ImportController.ToEntity(new BookLookupResult
        {
            ISBN = "9784101010014",
            Title = "吾輩は猫である"
        });

        Assert.Equal(DateTime.Today, entity.AcquisitionDate);
    }

    [Fact]
    public void IsbnImportResultViewModel_DefaultContinueAction_IsIsbn()
    {
        Assert.Equal("Isbn", new IsbnImportResultViewModel().ContinueAction);
    }

    [Fact]
    public void IsbnImportSnapshot_SourceAction_CanPreserveTitleSearchOrigin()
    {
        var snapshot = new IsbnImportSnapshot { SourceAction = "TitleSearch" };

        Assert.Equal("TitleSearch", snapshot.SourceAction);
    }

    [Fact]
    public async Task TitleSearch_NdlSearchFailed_SetsSearchFailed()
    {
        var ndl = new Mock<INdlSearchService>();
        ndl.Setup(x => x.SearchAsync(
                It.Is<NdlSearchCriteria>(c => c.Title == "吾輩は猫である"),
                1,
                ImportController.TitleSearchPageSize))
            .ReturnsAsync(new NdlSearchResponse
            {
                Succeeded = false
            });
        var controller = CreateImportControllerForTitleSearch(ndl.Object);

        var result = await controller.TitleSearch(new TitleSearchViewModel
        {
            Searched = true,
            Title = "吾輩は猫である",
            SortOrder = NdlSearchSortOrder.Default
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TitleSearchViewModel>(view.Model);
        Assert.True(model.HasSearched);
        Assert.True(model.SearchFailed);
        Assert.Empty(model.Results);
        ndl.Verify(x => x.SearchAsync(
                It.Is<NdlSearchCriteria>(c => c.Title == "吾輩は猫である"),
                1,
                ImportController.TitleSearchPageSize),
            Times.Once);
    }

    [Fact]
    public void ParseTitleSearchSelections_SelectedMatchingRecord_ReturnsBook()
    {
        const string json = """
            [{"ISBN":"9784101010014","Title":"検索時タイトル","Creator":"著者","Publisher":"出版社","PublicationDate":"2024"}]
            """;

        var result = ImportController.ParseTitleSearchSelections(
            json,
            ["9784101010014"]);

        var book = Assert.Single(result).Value;
        Assert.Equal("検索時タイトル", book.Title);
        Assert.Equal(new DateTime(2024, 1, 1), book.ReleaseDate);
    }

    [Fact]
    public void ParseTitleSearchSelections_UnselectedOrInvalidRecord_IsIgnored()
    {
        const string json = """
            [
              {"ISBN":"9784101010014","Title":"未選択"},
              {"ISBN":"invalid","Title":"不正ISBN"},
              {"ISBN":"9784003101018","Title":""}
            ]
            """;

        var result = ImportController.ParseTitleSearchSelections(
            json,
            ["9784003101018"]);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseTitleSearchSelections_OverlongTitle_IsIgnored()
    {
        var json = $$"""[{"ISBN":"9784101010014","Title":"{{new string('長', 501)}}"}]""";

        var result = ImportController.ParseTitleSearchSelections(
            json,
            ["9784101010014"]);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseNoIsbnSelections_SelectedMatchingRecord_ReturnsBook()
    {
        const string json = """
            [{"Key":"noisbn:1:0","Title":"ISBNなし書籍","Creator":"著者","Publisher":"出版社","PublicationDate":"2024"}]
            """;

        var result = ImportController.ParseNoIsbnSelections(json, ["noisbn:1:0"]);

        var book = Assert.Single(result);
        Assert.Equal("ISBNなし書籍", book.Title);
        Assert.Null(book.ISBN);
        Assert.Equal(new DateTime(2024, 1, 1), book.ReleaseDate);
    }

    [Fact]
    public void ParseNoIsbnSelections_UnselectedOrInvalidRecord_IsIgnored()
    {
        const string json = """
            [
              {"Key":"noisbn:1:0","Title":"未選択"},
              {"Key":"noisbn:1:1","Title":""}
            ]
            """;

        var result = ImportController.ParseNoIsbnSelections(json, ["noisbn:1:1"]);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseNoIsbnSelections_OverlongTitle_IsIgnored()
    {
        var json = $$"""[{"Key":"noisbn:1:0","Title":"{{new string('長', 501)}}"}]""";

        var result = ImportController.ParseNoIsbnSelections(json, ["noisbn:1:0"]);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData("invalid", null, false, "無効なISBNです。")]
    [InlineData("9784101010014", null, true, "書誌情報の取得に失敗しました。しばらくしてから再度お試しください。")]
    [InlineData("9784101010014", null, false, "書誌情報が見つかりません。")]
    [InlineData("9784101010014", "タイトルを取得できません。", true, "タイトルを取得できません。")]
    public void BuildLookupErrorMessage_ReturnsSpecificUserFacingMessage(
        string input,
        string? validationError,
        bool ndlLookupFailed,
        string expected)
    {
        var result = ImportController.BuildLookupErrorMessage(
            input,
            validationError,
            ndlLookupFailed);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, "検索時タイトル")]
    [InlineData(1, "ISBN再検索タイトル")]
    public void ResolveSelectedBook_UsesRequestedNdlBibliographicRecord(
        int selectedIndex,
        string expectedTitle)
    {
        var row = new IsbnImportSnapshotRow
        {
            Key = "9784101010014",
            ISBN = "9784101010014",
            Candidates =
            [
                new BookLookupResult
                {
                    ISBN = "9784101010014",
                    Title = "検索時タイトル"
                },
                new BookLookupResult
                {
                    ISBN = "9784101010014",
                    Title = "ISBN再検索タイトル"
                }
            ]
        };

        var result = ImportController.ResolveSelectedBook(
            row,
            new Dictionary<string, int>
            {
                ["9784101010014"] = selectedIndex
            });

        Assert.Equal(expectedTitle, result?.Title);
    }

    [Fact]
    public void ResolveSelectedBook_MissingChoiceForTwoCandidates_ReturnsNull()
    {
        var row = new IsbnImportSnapshotRow
        {
            Key = "9784101010014",
            ISBN = "9784101010014",
            Candidates =
            [
                new BookLookupResult
                {
                    ISBN = "9784101010014",
                    Title = "ISBN再検索タイトル"
                },
                new BookLookupResult
                {
                    ISBN = "9784101010014",
                    Title = "検索時タイトル"
                }
            ]
        };

        var result = ImportController.ResolveSelectedBook(
            row,
            new Dictionary<string, int>());

        Assert.Null(result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void ResolveSelectedBook_OutOfRangeChoice_ReturnsNull(int selectedIndex)
    {
        var row = new IsbnImportSnapshotRow
        {
            Key = "9784101010014",
            ISBN = "9784101010014",
            Candidates =
            [
                new BookLookupResult
                {
                    ISBN = "9784101010014",
                    Title = "候補"
                }
            ]
        };

        var result = ImportController.ResolveSelectedBook(
            row,
            new Dictionary<string, int>
            {
                ["9784101010014"] = selectedIndex
            });

        Assert.Null(result);
    }

    [Fact]
    public void ResolveSelectedBook_CandidateWithDifferentIsbn_ReturnsNull()
    {
        var row = new IsbnImportSnapshotRow
        {
            Key = "9784101010014",
            ISBN = "9784101010014",
            Candidates =
            [
                new BookLookupResult
                {
                    ISBN = "9784003101018",
                    Title = "別ISBNの候補"
                }
            ]
        };

        var result = ImportController.ResolveSelectedBook(
            row,
            new Dictionary<string, int>
            {
                ["9784101010014"] = 0
            });

        Assert.Null(result);
    }

    [Fact]
    public void ResolveSelectedBook_NoIsbnRow_ReturnsSingleCandidate()
    {
        var row = new IsbnImportSnapshotRow
        {
            Key = "noisbn:1:0",
            ISBN = null,
            Candidates =
            [
                new BookLookupResult
                {
                    ISBN = null,
                    Title = "ISBNなし書籍"
                }
            ]
        };

        var result = ImportController.ResolveSelectedBook(
            row,
            new Dictionary<string, int>
            {
                ["noisbn:1:0"] = 0
            });

        Assert.Equal("ISBNなし書籍", result?.Title);
    }

    [Fact]
    public void ToEntity_KindleImport_UsesRegistrationDateInsteadOfSourceDate()
    {
        var entity = ImportController.ToEntity(new KindleImportRow
        {
            ASIN = "B000000001",
            Title = "Kindle Book",
            AcquiredTime = new DateTime(2020, 1, 2)
        });

        Assert.Equal(DateTime.Today, entity.AcquisitionDate);
    }

    [Theory]
    [InlineData("9784101010014", "4101010013")]
    [InlineData("4101010013", "4101010013")]
    [InlineData("9791234567896", null)]
    [InlineData("invalid", null)]
    public void ToAmazonAsinCandidate_ReturnsIsbn10CandidateFor978Books(
        string isbn,
        string? expected)
    {
        Assert.Equal(expected, OpenBDLookupService.ToAmazonAsinCandidate(isbn));
    }

    [Fact]
    public async Task ReadIsbnFileAsync_ValidUtf8Text_ReturnsLineSeparatedValues()
    {
        var file = CreateFile(
            "isbn.txt",
            Encoding.UTF8.GetBytes("9784101010014\r\n\r\n9784003101018\n"));

        var result = await ImportController.ReadIsbnFileAsync(file);

        Assert.Equal(
            $"9784101010014{Environment.NewLine}9784003101018",
            result);
    }

    [Theory]
    [InlineData("isbn.json", "9784101010014", "TXTまたはCSV")]
    [InlineData("isbn.csv", "ISBN\r\n9784101010014", "ヘッダー行")]
    [InlineData("isbn.txt", "9784101010014,9784003101018", "1行にISBNを1件")]
    public async Task ReadIsbnFileAsync_InvalidFormat_ThrowsUserFacingError(
        string fileName,
        string content,
        string expectedMessage)
    {
        var file = CreateFile(fileName, Encoding.UTF8.GetBytes(content));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => ImportController.ReadIsbnFileAsync(file));

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public async Task ReadIsbnFileAsync_InvalidUtf8_ThrowsUserFacingError()
    {
        var file = CreateFile("isbn.txt", [0xFF, 0xFE, 0xFF]);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => ImportController.ReadIsbnFileAsync(file));

        Assert.Contains("UTF-8", exception.Message);
    }

    [Fact]
    public void ToEntity_KindleImportWithEnrichment_MapsBookLookupFields()
    {
        var entity = ImportController.ToEntity(new KindleImportRow
        {
            ASIN = "4003101014",
            Title = "こころ",
            Creator = "夏目 漱石",
            ISBN = "9784003101018",
            Publisher = "岩波書店",
            ReleaseDate = new DateTime(1927, 1, 1),
            PageCount = 376,
            ThumbnailUrl = "https://example.test/cover.jpg"
        });

        Assert.Equal("こころ", entity.Title);
        Assert.Equal("夏目 漱石", entity.Creator);
        Assert.Equal("4003101014", entity.ASIN);
        Assert.Equal("9784003101018", entity.ISBN);
        Assert.Equal("岩波書店", entity.Publisher);
        Assert.Equal(new DateTime(1927, 1, 1), entity.ReleaseDate);
        Assert.Equal(376, entity.PageCount);
        Assert.True(entity.IsDigital);
        Assert.Equal(DateTime.Today, entity.AcquisitionDate);
    }

    [Fact]
    public void ToEntity_KindleImportWithoutEnrichment_LeavesBookLookupFieldsNull()
    {
        var entity = ImportController.ToEntity(new KindleImportRow
        {
            ASIN = "B000000001",
            Title = "Kindle Only Book",
            Creator = "Author"
        });

        Assert.Null(entity.ISBN);
        Assert.Null(entity.Publisher);
        Assert.Null(entity.ReleaseDate);
        Assert.Null(entity.PageCount);
    }

    [Fact]
    public async Task EnrichWithBookLookupAsync_NdlLookupFailed_MarksRowAndKeepsSourceTitle()
    {
        var candidateService = new Mock<IBookCandidateLookupService>();
        candidateService
            .Setup(x => x.LookupCandidatesByIsbnsAsync(
                It.Is<IReadOnlyList<string>>(isbns => isbns.Contains("9784003101018"))))
            .ReturnsAsync((IReadOnlyList<string> isbns) => isbns
                .Select(_ => new BookLookupCandidates { Results = [], NdlLookupFailed = true })
                .ToList());
        var controller = CreateImportController(candidateLookupService: candidateService.Object);
        var rows = new List<KindleImportRow>
        {
            new() { ASIN = "4003101014", Title = "こころ（Kindle元タイトル）" }
        };

        await controller.EnrichWithBookLookupAsync(rows);

        Assert.True(rows[0].EnrichmentFailed);
        Assert.Equal("こころ（Kindle元タイトル）", rows[0].Title);
        Assert.Null(rows[0].ISBN);
    }

    [Fact]
    public async Task EnrichWithBookLookupAsync_NdlLookupSucceeded_FillsBookFieldsAndDoesNotMarkFailure()
    {
        var candidateService = new Mock<IBookCandidateLookupService>();
        candidateService
            .Setup(x => x.LookupCandidatesByIsbnsAsync(
                It.Is<IReadOnlyList<string>>(isbns => isbns.Contains("9784003101018"))))
            .ReturnsAsync((IReadOnlyList<string> isbns) => isbns
                .Select(_ => new BookLookupCandidates
                {
                    Results =
                    [
                        new BookLookupResult
                        {
                            ISBN = "9784003101018",
                            Title = "こころ",
                            Creator = "夏目 漱石",
                            Publisher = "岩波書店"
                        }
                    ],
                    NdlLookupFailed = false
                })
                .ToList());
        var controller = CreateImportController(candidateLookupService: candidateService.Object);
        var rows = new List<KindleImportRow>
        {
            new() { ASIN = "4003101014", Title = "こころ（Kindle元タイトル）" }
        };

        await controller.EnrichWithBookLookupAsync(rows);

        Assert.False(rows[0].EnrichmentFailed);
        Assert.Equal("こころ", rows[0].Title);
        Assert.Equal("9784003101018", rows[0].ISBN);
    }

    [Fact]
    public async Task EnrichWithBookLookupAsync_LookupThrows_MarksTargetRowsAsFailedAndKeepsRows()
    {
        var candidateService = new Mock<IBookCandidateLookupService>();
        candidateService
            .Setup(x => x.LookupCandidatesByIsbnsAsync(It.IsAny<IReadOnlyList<string>>()))
            .ThrowsAsync(new HttpRequestException("lookup unavailable"));
        var controller = CreateImportController(candidateLookupService: candidateService.Object);
        var rows = new List<KindleImportRow>
        {
            new() { ASIN = "4003101014", Title = "こころ（Kindle元タイトル）" }
        };

        await controller.EnrichWithBookLookupAsync(rows);

        Assert.True(rows[0].EnrichmentFailed);
        Assert.Equal("こころ（Kindle元タイトル）", rows[0].Title);
    }

    [Theory]
    [InlineData("4003101014", true)]
    [InlineData("080442957X", true)]
    [InlineData("B012345678", false)]
    [InlineData("B0ABCDEFGH", false)]
    public void NormalizeIsbn13_DistinguishesIsbn10FromKindleAsin(
        string asin,
        bool shouldResolve)
    {
        var result = OpenBDLookupService.NormalizeIsbn13(asin);
        Assert.Equal(shouldResolve, result != null);
    }

    [Theory]
    [InlineData("９７８４１０１０１００１４", "9784101010014")]
    [InlineData("978-4-101-01001-4", "9784101010014")]
    [InlineData("９７８－４－１０１－０１００１－４", "9784101010014")]
    [InlineData("４００３１０１０１４", "9784003101018")]
    public void NormalizeIsbn13_FullWidthAndHyphens_NormalizesCorrectly(
        string input,
        string expected)
    {
        Assert.Equal(expected, OpenBDLookupService.NormalizeIsbn13(input));
    }

    [Fact]
    public void IsbnImportSnapshot_FailedInputs_DefaultsToEmptyList()
    {
        var snapshot = new IsbnImportSnapshot();

        Assert.NotNull(snapshot.FailedInputs);
        Assert.Empty(snapshot.FailedInputs);
    }

    [Fact]
    public void IsbnImportSnapshot_FailedInputs_RoundTripsWithJsonSerialization()
    {
        var snapshot = new IsbnImportSnapshot
        {
            SourceAction = "Isbn",
            FailedInputs = ["9784101010014", "9784003101018"],
            Rows =
            [
                new IsbnImportSnapshotRow
                {
                    Key = "9784000000000",
                    ISBN = "9784000000000",
                    Candidates = [new BookLookupResult { ISBN = "9784000000000", Title = "成功した本" }]
                }
            ]
        };

        var json = System.Text.Json.JsonSerializer.Serialize(snapshot);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<IsbnImportSnapshot>(json)!;

        Assert.Equal(2, deserialized.FailedInputs.Count);
        Assert.Contains("9784101010014", deserialized.FailedInputs);
        Assert.Contains("9784003101018", deserialized.FailedInputs);
        Assert.Single(deserialized.Rows);
    }

    [Fact]
    public void IsbnImportRow_NdlLookupFailed_DefaultsToFalse()
    {
        var row = new IsbnImportRow();
        Assert.False(row.NdlLookupFailed);
    }

    [Fact]
    public void MergeRefetchResults_SuccessfulRefetch_AddsToNewRowsAndRemovesFromStillFailed()
    {
        var failedInputs = new List<string> { "9784101010014" };
        var candidateLookups = new List<BookLookupCandidates>
        {
            new()
            {
                Results = [new BookLookupResult { ISBN = "9784101010014", Title = "こころ" }],
                NdlLookupFailed = false
            }
        };
        var existingIsbns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dbRegisteredIsbns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var result = ImportController.MergeRefetchResults(
            failedInputs, candidateLookups, existingIsbns, dbRegisteredIsbns);

        Assert.Single(result.NewRows);
        Assert.Equal("9784101010014", result.NewRows[0].ISBN);
        Assert.Equal("こころ", result.NewRows[0].Candidates[0].Title);
        Assert.Empty(result.StillFailed);
    }

    [Fact]
    public void MergeRefetchResults_StillFailed_RemainsInStillFailed()
    {
        var failedInputs = new List<string> { "9784101010014" };
        var candidateLookups = new List<BookLookupCandidates>
        {
            new() { Results = [], NdlLookupFailed = true }
        };
        var existingIsbns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dbRegisteredIsbns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var result = ImportController.MergeRefetchResults(
            failedInputs, candidateLookups, existingIsbns, dbRegisteredIsbns);

        Assert.Empty(result.NewRows);
        Assert.Single(result.StillFailed);
        Assert.Equal("9784101010014", result.StillFailed[0]);
    }

    [Fact]
    public void MergeRefetchResults_DuplicateInSnapshot_DroppedFromBothLists()
    {
        var failedInputs = new List<string> { "9784101010014" };
        var candidateLookups = new List<BookLookupCandidates>
        {
            new()
            {
                Results = [new BookLookupResult { ISBN = "9784101010014", Title = "こころ" }],
                NdlLookupFailed = false
            }
        };
        // スナップショットに既に存在するISBN
        var existingIsbns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "9784101010014" };
        var dbRegisteredIsbns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var result = ImportController.MergeRefetchResults(
            failedInputs, candidateLookups, existingIsbns, dbRegisteredIsbns);

        Assert.Empty(result.NewRows);
        Assert.Empty(result.StillFailed);
    }

    [Fact]
    public void MergeRefetchResults_DuplicateInDb_DroppedFromBothLists()
    {
        var failedInputs = new List<string> { "9784101010014" };
        var candidateLookups = new List<BookLookupCandidates>
        {
            new()
            {
                Results = [new BookLookupResult { ISBN = "9784101010014", Title = "こころ" }],
                NdlLookupFailed = false
            }
        };
        var existingIsbns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // DBに既に登録済みのISBN
        var dbRegisteredIsbns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "9784101010014" };

        var result = ImportController.MergeRefetchResults(
            failedInputs, candidateLookups, existingIsbns, dbRegisteredIsbns);

        Assert.Empty(result.NewRows);
        Assert.Empty(result.StillFailed);
    }

    [Fact]
    public void MergeRefetchResults_MixedResults_PartialSuccessAndFailure()
    {
        var failedInputs = new List<string> { "9784101010014", "9784003101018" };
        var candidateLookups = new List<BookLookupCandidates>
        {
            new()
            {
                Results = [new BookLookupResult { ISBN = "9784101010014", Title = "成功した本" }],
                NdlLookupFailed = false
            },
            new() { Results = [], NdlLookupFailed = true }
        };
        var existingIsbns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dbRegisteredIsbns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var result = ImportController.MergeRefetchResults(
            failedInputs, candidateLookups, existingIsbns, dbRegisteredIsbns);

        Assert.Single(result.NewRows);
        Assert.Equal("9784101010014", result.NewRows[0].ISBN);
        Assert.Single(result.StillFailed);
        Assert.Equal("9784003101018", result.StillFailed[0]);
    }

    private static FormFile CreateFile(string fileName, byte[] bytes)
    {
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", fileName);
    }

    private static ImportController CreateImportControllerForTitleSearch(
        INdlSearchService ndlSearchService)
    {
        return CreateImportController(ndlSearchService: ndlSearchService);
    }

    private static ImportController CreateImportController(
        INdlSearchService? ndlSearchService = null,
        IBookCandidateLookupService? candidateLookupService = null)
    {
        var options = new DbContextOptionsBuilder<DBContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=KototsuboImportControllerTests;Trusted_Connection=True;")
            .Options;
        var repository = new Mock<ItemRepository>(new DBContext(options));
        repository.Setup(x => x.GetBaseQuery(null, false))
            .Returns(Array.Empty<ItemEntity>().AsQueryable());

        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(x => x.ContentRootPath).Returns(Path.GetTempPath());

        return new ImportController(
            repository.Object,
            new KindleImportParser(),
            candidateLookupService ?? Mock.Of<IBookCandidateLookupService>(),
            ndlSearchService ?? Mock.Of<INdlSearchService>(),
            environment.Object,
            NullLogger<ImportController>.Instance);
    }
}
