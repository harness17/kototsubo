using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Site.Services;
using Xunit;

namespace Tests;

public class NdlPrimaryBookLookupServiceTests
{
    [Fact]
    public async Task LookupByIsbnAsync_OpenBdHasNoRecord_UsesNdlFields()
    {
        var ndl = new Mock<INdlSearchService>();
        ndl.Setup(x => x.SearchAsync(
                It.Is<NdlSearchCriteria>(c => c.ISBN == "9784101010014"),
                1,
                10))
            .ReturnsAsync(new NdlSearchResponse
            {
                Results =
                [
                    new NdlSearchResult
                    {
                        Title = "NDLタイトル",
                        Creator = "NDL著者",
                        Publisher = "NDL出版社",
                        ISBN = "4101010013",
                        PublicationDate = "1971"
                    }
                ]
            });
        var service = CreateService("[null]", ndl.Object);

        var result = await service.LookupByIsbnAsync("9784101010014");

        Assert.NotNull(result);
        Assert.Equal("NDLタイトル", result.Title);
        Assert.Equal("NDL著者", result.Creator);
        Assert.Equal("NDL出版社", result.Publisher);
        Assert.Equal(new DateTime(1971, 1, 1), result.ReleaseDate);
        Assert.Equal("9784101010014", result.ISBN);
    }

    [Fact]
    public async Task LookupByIsbnAsync_BothSourcesHaveRecord_PrefersNdlBibliographicFields()
    {
        const string openBdJson = """
            [{
              "summary": {
                "title": "openBDタイトル",
                "publisher": "openBD出版社",
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
        var ndl = new Mock<INdlSearchService>();
        ndl.Setup(x => x.SearchAsync(
                It.IsAny<NdlSearchCriteria>(),
                1,
                10))
            .ReturnsAsync(new NdlSearchResponse
            {
                Results =
                [
                    new NdlSearchResult
                    {
                        Title = "NDLタイトル",
                        Creator = "NDL著者",
                        Publisher = "NDL出版社",
                        ISBN = "9784101010014",
                        PublicationDate = "2003-06"
                    }
                ]
            });
        var service = CreateService(openBdJson, ndl.Object);

        var result = await service.LookupByIsbnAsync("9784101010014");

        Assert.NotNull(result);
        Assert.Equal("NDLタイトル", result.Title);
        Assert.Equal("NDL著者", result.Creator);
        Assert.Equal("NDL出版社", result.Publisher);
        Assert.Equal(new DateTime(2003, 6, 1), result.ReleaseDate);
        Assert.Equal("https://example.test/cover.jpg", result.CoverImageUrl);
        Assert.Equal(320, result.PageCount);
    }

    [Fact]
    public async Task LookupByIsbnAsync_AllOpenBdFieldsExist_StillUsesNdlAsPrimarySource()
    {
        const string openBdJson = """
            [{
              "summary": {
                "title": "openBDタイトル",
                "author": "openBD著者",
                "publisher": "openBD出版社",
                "pubdate": "20030615",
                "isbn": "9784101010014"
              }
            }]
            """;
        var ndl = new Mock<INdlSearchService>();
        ndl.Setup(x => x.SearchAsync(
                It.IsAny<NdlSearchCriteria>(),
                1,
                10))
            .ReturnsAsync(new NdlSearchResponse
            {
                Results =
                [
                    new NdlSearchResult
                    {
                        Title = "NDLタイトル",
                        Creator = "NDL著者",
                        Publisher = "NDL出版社",
                        ISBN = "9784101010014",
                        PublicationDate = "1971"
                    }
                ]
            });
        var service = CreateService(openBdJson, ndl.Object);

        var result = await service.LookupByIsbnAsync("9784101010014");

        Assert.NotNull(result);
        Assert.Equal("NDLタイトル", result.Title);
        ndl.Verify(
            x => x.SearchAsync(
                It.IsAny<NdlSearchCriteria>(),
                It.IsAny<int>(),
                It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task LookupByIsbnAsync_NdlFieldsAreMissing_UsesOpenBdValuesForThoseFields()
    {
        const string openBdJson = """
            [{
              "summary": {
                "title": "openBDタイトル",
                "author": "openBD著者",
                "publisher": "openBD出版社",
                "pubdate": "20030615",
                "isbn": "9784101010014"
              }
            }]
            """;
        var ndl = new Mock<INdlSearchService>();
        ndl.Setup(x => x.SearchAsync(
                It.IsAny<NdlSearchCriteria>(),
                1,
                10))
            .ReturnsAsync(new NdlSearchResponse
            {
                Results =
                [
                    new NdlSearchResult
                    {
                        Title = "NDLタイトル",
                        ISBN = "9784101010014"
                    }
                ]
            });
        var service = CreateService(openBdJson, ndl.Object);

        var result = await service.LookupByIsbnAsync("9784101010014");

        Assert.NotNull(result);
        Assert.Equal("NDLタイトル", result.Title);
        Assert.Equal("openBD著者", result.Creator);
        Assert.Equal("openBD出版社", result.Publisher);
        Assert.Equal(new DateTime(2003, 6, 15), result.ReleaseDate);
    }

    [Fact]
    public async Task LookupByIsbnAsync_NdlReturnsDifferentIsbn_KeepsOpenBdRecord()
    {
        const string openBdJson = """
            [{
              "summary": {
                "title": "openBDタイトル",
                "author": "openBD著者",
                "publisher": "openBD出版社",
                "pubdate": "20030615",
                "isbn": "9784101010014"
              }
            }]
            """;
        var ndl = new Mock<INdlSearchService>();
        ndl.Setup(x => x.SearchAsync(
                It.IsAny<NdlSearchCriteria>(),
                1,
                10))
            .ReturnsAsync(new NdlSearchResponse
            {
                Results =
                [
                    new NdlSearchResult
                    {
                        Title = "別ISBNのNDLタイトル",
                        ISBN = "9784003101018"
                    }
                ]
            });
        var service = CreateService(openBdJson, ndl.Object);

        var result = await service.LookupByIsbnAsync("9784101010014");

        Assert.NotNull(result);
        Assert.Equal("openBDタイトル", result.Title);
        Assert.Equal("9784101010014", result.ISBN);
    }

    [Fact]
    public async Task LookupByIsbnAsync_NdlThrows_KeepsOpenBdRecord()
    {
        const string openBdJson = """
            [{
              "summary": {
                "title": "openBDタイトル",
                "isbn": "9784101010014"
              }
            }]
            """;
        var ndl = new Mock<INdlSearchService>();
        ndl.Setup(x => x.SearchAsync(
                It.IsAny<NdlSearchCriteria>(),
                1,
                10))
            .ThrowsAsync(new HttpRequestException("NDL unavailable"));
        var service = CreateService(openBdJson, ndl.Object);

        var result = await service.LookupByIsbnAsync("9784101010014");

        Assert.NotNull(result);
        Assert.Equal("openBDタイトル", result.Title);
    }

    [Fact]
    public async Task LookupByIsbnAsync_BothSourcesHaveNoRecord_ReturnsNull()
    {
        var ndl = new Mock<INdlSearchService>();
        ndl.Setup(x => x.SearchAsync(
                It.IsAny<NdlSearchCriteria>(),
                1,
                10))
            .ReturnsAsync(new NdlSearchResponse());
        var service = CreateService("[null]", ndl.Object);

        var result = await service.LookupByIsbnAsync("9784101010014");

        Assert.Null(result);
    }

    [Fact]
    public async Task LookupCandidatesByIsbnAsync_MultipleMatchingNdlRecords_ReturnsAll()
    {
        var ndl = new Mock<INdlSearchService>();
        ndl.Setup(x => x.SearchAsync(
                It.Is<NdlSearchCriteria>(c => c.ISBN == "9784101010014"),
                1,
                20))
            .ReturnsAsync(new NdlSearchResponse
            {
                Results =
                [
                    new NdlSearchResult
                    {
                        ISBN = "9784101010014",
                        Title = "候補A",
                        Creator = "著者A"
                    },
                    new NdlSearchResult
                    {
                        ISBN = "4101010013",
                        Title = "候補B",
                        Creator = "著者B"
                    }
                ]
            });
        var service = CreateService("[null]", ndl.Object);

        var results = await service.LookupCandidatesByIsbnAsync("9784101010014");

        Assert.False(results.NdlLookupFailed);
        Assert.Equal(2, results.Results.Count);
        Assert.Equal(["候補A", "候補B"], results.Results.Select(x => x.Title));
    }

    [Fact]
    public async Task LookupCandidatesByIsbnAsync_DifferentIsbnAndDuplicate_AreExcluded()
    {
        var ndl = new Mock<INdlSearchService>();
        ndl.Setup(x => x.SearchAsync(
                It.IsAny<NdlSearchCriteria>(),
                1,
                20))
            .ReturnsAsync(new NdlSearchResponse
            {
                Results =
                [
                    new NdlSearchResult
                    {
                        ISBN = "9784101010014",
                        Title = "候補A"
                    },
                    new NdlSearchResult
                    {
                        ISBN = "4101010013",
                        Title = "候補A"
                    },
                    new NdlSearchResult
                    {
                        ISBN = "9784003101018",
                        Title = "別ISBN"
                    }
                ]
            });
        var service = CreateService("[null]", ndl.Object);

        var results = await service.LookupCandidatesByIsbnAsync("9784101010014");

        Assert.False(results.NdlLookupFailed);
        Assert.Single(results.Results);
        Assert.Equal("候補A", results.Results[0].Title);
    }

    [Fact]
    public async Task LookupCandidatesByIsbnAsync_NoMatchingNdlRecord_ReturnsOpenBd()
    {
        const string openBdJson = """
            [{
              "summary": {
                "title": "openBDタイトル",
                "isbn": "9784101010014"
              }
            }]
            """;
        var ndl = new Mock<INdlSearchService>();
        ndl.Setup(x => x.SearchAsync(
                It.IsAny<NdlSearchCriteria>(),
                1,
                20))
            .ReturnsAsync(new NdlSearchResponse());
        var service = CreateService(openBdJson, ndl.Object);

        var results = await service.LookupCandidatesByIsbnAsync("9784101010014");

        Assert.False(results.NdlLookupFailed);
        var result = Assert.Single(results.Results);
        Assert.Equal("openBDタイトル", result.Title);
    }

    [Fact]
    public async Task LookupCandidatesByIsbnAsync_NdlThrows_ReturnsOpenBd()
    {
        const string openBdJson = """
            [{
              "summary": {
                "title": "openBDタイトル",
                "isbn": "9784101010014"
              }
            }]
            """;
        var ndl = new Mock<INdlSearchService>();
        ndl.Setup(x => x.SearchAsync(
                It.IsAny<NdlSearchCriteria>(),
                1,
                20))
            .ThrowsAsync(new HttpRequestException("NDL unavailable"));
        var service = CreateService(openBdJson, ndl.Object);

        var results = await service.LookupCandidatesByIsbnAsync("9784101010014");

        Assert.True(results.NdlLookupFailed);
        var result = Assert.Single(results.Results);
        Assert.Equal("openBDタイトル", result.Title);
    }

    [Fact]
    public async Task LookupCandidatesByIsbnAsync_NdlFailedAndOpenBdEmpty_ReturnsFailureFlag()
    {
        var ndl = new Mock<INdlSearchService>();
        ndl.Setup(x => x.SearchAsync(
                It.IsAny<NdlSearchCriteria>(),
                1,
                20))
            .ReturnsAsync(new NdlSearchResponse { Succeeded = false });
        var service = CreateService("[null]", ndl.Object);

        var results = await service.LookupCandidatesByIsbnAsync("9784101010014");

        Assert.True(results.NdlLookupFailed);
        Assert.Empty(results.Results);
    }

    [Fact]
    public async Task LookupCandidatesByIsbnAsync_NdlFailedAndOpenBdHasRecord_ReturnsOpenBdWithFailureFlag()
    {
        const string openBdJson = """
            [{
              "summary": {
                "title": "openBDタイトル",
                "isbn": "9784101010014"
              }
            }]
            """;
        var ndl = new Mock<INdlSearchService>();
        ndl.Setup(x => x.SearchAsync(
                It.IsAny<NdlSearchCriteria>(),
                1,
                20))
            .ReturnsAsync(new NdlSearchResponse { Succeeded = false });
        var service = CreateService(openBdJson, ndl.Object);

        var results = await service.LookupCandidatesByIsbnAsync("9784101010014");

        Assert.True(results.NdlLookupFailed);
        var result = Assert.Single(results.Results);
        Assert.Equal("openBDタイトル", result.Title);
    }

    [Fact]
    public async Task LookupCandidatesByIsbnAsync_NdlNormalEmptyAndOpenBdEmpty_DoesNotSetFailureFlag()
    {
        var ndl = new Mock<INdlSearchService>();
        ndl.Setup(x => x.SearchAsync(
                It.IsAny<NdlSearchCriteria>(),
                1,
                20))
            .ReturnsAsync(new NdlSearchResponse());
        var service = CreateService("[null]", ndl.Object);

        var results = await service.LookupCandidatesByIsbnAsync("9784101010014");

        Assert.False(results.NdlLookupFailed);
        Assert.Empty(results.Results);
    }

    [Fact]
    public async Task LookupCandidatesByIsbnAsync_InvalidIsbn_DoesNotCallApis()
    {
        var ndl = new Mock<INdlSearchService>();
        var service = CreateService("[null]", ndl.Object);

        var results = await service.LookupCandidatesByIsbnAsync("invalid");

        Assert.False(results.NdlLookupFailed);
        Assert.Empty(results.Results);
        ndl.Verify(
            x => x.SearchAsync(
                It.IsAny<NdlSearchCriteria>(),
                It.IsAny<int>(),
                It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task LookupCandidatesByIsbnsAsync_MultipleIsbns_KeepsInputOrderAndCandidates()
    {
        var ndl = new Mock<INdlSearchService>();
        ndl.Setup(x => x.SearchAsync(
                It.IsAny<NdlSearchCriteria>(),
                1,
                20))
            .ReturnsAsync((NdlSearchCriteria criteria, int _, int _) =>
                new NdlSearchResponse
                {
                    Results =
                    [
                        new NdlSearchResult
                        {
                            ISBN = criteria.ISBN,
                            Title = criteria.ISBN == "9784101010014"
                                ? "一冊目A"
                                : "二冊目A"
                        },
                        new NdlSearchResult
                        {
                            ISBN = criteria.ISBN,
                            Title = criteria.ISBN == "9784101010014"
                                ? "一冊目B"
                                : "二冊目B"
                        }
                    ]
                });
        var service = CreateService("[null,null]", ndl.Object);

        var results = await service.LookupCandidatesByIsbnsAsync(
            ["9784101010014", "9784003101018"]);

        Assert.Equal(2, results.Count);
        Assert.False(results[0].NdlLookupFailed);
        Assert.False(results[1].NdlLookupFailed);
        Assert.Equal(["一冊目A", "一冊目B"], results[0].Results.Select(x => x.Title));
        Assert.Equal(["二冊目A", "二冊目B"], results[1].Results.Select(x => x.Title));
    }

    private static NdlPrimaryBookLookupService CreateService(
        string openBdJson,
        INdlSearchService ndlSearch)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new StubHandler(openBdJson)));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExternalApis:OpenBDBaseUrl"] = "https://example.test/openbd"
            })
            .Build();
        var openBd = new OpenBDLookupService(
            factory.Object,
            NullLogger<OpenBDLookupService>.Instance,
            configuration);

        return new NdlPrimaryBookLookupService(
            openBd,
            ndlSearch,
            NullLogger<NdlPrimaryBookLookupService>.Instance);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _content;

        public StubHandler(string content)
        {
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_content)
            });
        }
    }
}
