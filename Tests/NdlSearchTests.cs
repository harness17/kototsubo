using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Site.Models;
using Site.Services;
using Xunit;

namespace Tests;

public class NdlSearchTests
{
    private static NdlSearchService CreateService()
    {
        var config = new ConfigurationBuilder().Build();
        return new NdlSearchService(
            null!,
            NullLogger<NdlSearchService>.Instance,
            config);
    }

    private static NdlSearchService CreateService(HttpMessageHandler handler)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExternalApis:NdlBaseUrl"] = "https://example.test/ndl"
            })
            .Build();
        return new NdlSearchService(
            new StubHttpClientFactory(handler),
            NullLogger<NdlSearchService>.Instance,
            config);
    }

    [Fact]
    public void TitleSearchViewModel_OnInitialDisplay_DoesNotShowValidationError()
    {
        var model = new TitleSearchViewModel { Searched = false };
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true);

        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void TitleSearchViewModel_WhenSubmittedWithoutCriteria_ShowsValidationError()
    {
        var model = new TitleSearchViewModel { Searched = true };
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(
            results,
            result => result.ErrorMessage == "検索条件を1つ以上指定してください。");
    }

    [Fact]
    public void ParseResponse_WithIsbn_ExtractsAllFields()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<searchRetrieveResponse xmlns=""http://www.loc.gov/zing/srw/"">
  <version>1.2</version>
  <numberOfRecords>1</numberOfRecords>
  <records>
    <record>
      <recordSchema>info:srw/schema/1/dcndl</recordSchema>
      <recordPacking>xml</recordPacking>
      <recordData>
        <rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#""
                 xmlns:dcterms=""http://purl.org/dc/terms/""
                 xmlns:foaf=""http://xmlns.com/foaf/0.1/""
                 xmlns:dcndl=""http://ndl.go.jp/dcndl/terms/"">
          <dcndl:BibResource>
            <dcterms:title>掌の小説</dcterms:title>
            <dcterms:creator>
              <foaf:Agent><foaf:name>川端, 康成</foaf:name></foaf:Agent>
            </dcterms:creator>
            <dcterms:publisher>
              <foaf:Agent><foaf:name>新潮社</foaf:name></foaf:Agent>
            </dcterms:publisher>
            <dcterms:identifier rdf:datatype=""http://ndl.go.jp/dcndl/terms/ISBN"">4101001057</dcterms:identifier>
            <dcterms:issued rdf:datatype=""http://purl.org/dc/terms/W3CDTF"">1971</dcterms:issued>
          </dcndl:BibResource>
        </rdf:RDF>
      </recordData>
      <recordPosition>1</recordPosition>
    </record>
  </records>
</searchRetrieveResponse>";
        var doc = XDocument.Parse(xml);
        var service = CreateService();

        var result = service.ParseResponse(doc);

        Assert.Equal(1, result.TotalResults);
        Assert.Single(result.Results);
        var book = result.Results[0];
        Assert.Equal("掌の小説", book.Title);
        Assert.Equal("川端, 康成", book.Creator);
        Assert.Equal("新潮社", book.Publisher);
        Assert.Equal("4101001057", book.ISBN);
        Assert.Equal("1971", book.PublicationDate);
    }

    [Fact]
    public void ParseResponse_WithoutIsbn_ReturnsNullIsbn()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<searchRetrieveResponse xmlns=""http://www.loc.gov/zing/srw/"">
  <version>1.2</version>
  <numberOfRecords>1</numberOfRecords>
  <records>
    <record>
      <recordSchema>info:srw/schema/1/dcndl</recordSchema>
      <recordPacking>xml</recordPacking>
      <recordData>
        <rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#""
                 xmlns:dcterms=""http://purl.org/dc/terms/""
                 xmlns:foaf=""http://xmlns.com/foaf/0.1/""
                 xmlns:dcndl=""http://ndl.go.jp/dcndl/terms/"">
          <dcndl:BibResource>
            <dcterms:title>古い手稿</dcterms:title>
            <dcterms:identifier rdf:datatype=""http://ndl.go.jp/dcndl/terms/NDLBibID"">12345</dcterms:identifier>
          </dcndl:BibResource>
        </rdf:RDF>
      </recordData>
      <recordPosition>1</recordPosition>
    </record>
  </records>
</searchRetrieveResponse>";
        var doc = XDocument.Parse(xml);
        var service = CreateService();

        var result = service.ParseResponse(doc);

        Assert.Single(result.Results);
        Assert.Null(result.Results[0].ISBN);
    }

    [Fact]
    public void ParseResponse_WithStringPacking_ExtractsFields()
    {
        // NDL の実 API は recordPacking="string" で返し、recordData の中身が
        // XML エスケープされた文字列になる。これを再パースできることを検証する。
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<searchRetrieveResponse xmlns=""http://www.loc.gov/zing/srw/"">
  <version>1.2</version>
  <numberOfRecords>1</numberOfRecords>
  <records>
    <record>
      <recordSchema>info:srw/schema/1/dc-v1.1</recordSchema>
      <recordPacking>string</recordPacking>
      <recordData>
        &lt;rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#""
                 xmlns:dcterms=""http://purl.org/dc/terms/""
                 xmlns:foaf=""http://xmlns.com/foaf/0.1/""
                 xmlns:dcndl=""http://ndl.go.jp/dcndl/terms/""&gt;
          &lt;dcndl:BibResource&gt;
            &lt;dcterms:title&gt;吾輩は猫である&lt;/dcterms:title&gt;
            &lt;dcterms:creator&gt;
              &lt;foaf:Agent&gt;&lt;foaf:name&gt;夏目, 漱石&lt;/foaf:name&gt;&lt;/foaf:Agent&gt;
            &lt;/dcterms:creator&gt;
            &lt;dcterms:identifier rdf:datatype=""http://ndl.go.jp/dcndl/terms/ISBN""&gt;4895283690&lt;/dcterms:identifier&gt;
          &lt;/dcndl:BibResource&gt;
        &lt;/rdf:RDF&gt;
      </recordData>
      <recordPosition>1</recordPosition>
    </record>
  </records>
</searchRetrieveResponse>";
        var doc = XDocument.Parse(xml);
        var service = CreateService();

        var result = service.ParseResponse(doc);

        Assert.Single(result.Results);
        var book = result.Results[0];
        Assert.Equal("吾輩は猫である", book.Title);
        Assert.Equal("夏目, 漱石", book.Creator);
        Assert.Equal("4895283690", book.ISBN);
    }

    [Fact]
    public void ParseResponse_EmptyResponse_ReturnsEmptyList()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<searchRetrieveResponse xmlns=""http://www.loc.gov/zing/srw/"">
  <version>1.2</version>
  <numberOfRecords>0</numberOfRecords>
  <records/>
</searchRetrieveResponse>";
        var doc = XDocument.Parse(xml);
        var service = CreateService();

        var result = service.ParseResponse(doc);

        Assert.Equal(0, result.TotalResults);
        Assert.True(result.Succeeded);
        Assert.Empty(result.Results);
    }

    [Fact]
    public void ParseResponse_WithoutRoot_ReturnsFailure()
    {
        var service = CreateService();

        var result = service.ParseResponse(new XDocument());

        Assert.False(result.Succeeded);
        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task SearchAsync_TransientTimeout_RetriesOnceAndReturnsSuccess()
    {
        var handler = new SequenceHandler(
            (_, _) => Task.FromException<HttpResponseMessage>(
                new TaskCanceledException("timeout")),
            (_, _) => Task.FromResult(OkResponse(OneRecordXml())));
        var service = CreateService(handler);

        var result = await service.SearchAsync(new NdlSearchCriteria { ISBN = "9784101010014" });

        Assert.True(result.Succeeded);
        Assert.Single(result.Results);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task SearchAsync_TwoTransientTimeouts_ReturnsFailure()
    {
        var handler = new SequenceHandler(
            (_, _) => Task.FromException<HttpResponseMessage>(
                new TaskCanceledException("timeout")),
            (_, _) => Task.FromException<HttpResponseMessage>(
                new TaskCanceledException("timeout")));
        var service = CreateService(handler);

        var result = await service.SearchAsync(new NdlSearchCriteria { ISBN = "9784101010014" });

        Assert.False(result.Succeeded);
        Assert.Empty(result.Results);
        Assert.Equal(2, handler.CallCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task SearchAsync_TransientStatus_RetriesOnceAndReturnsSuccess(
        HttpStatusCode statusCode)
    {
        var handler = new SequenceHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(statusCode)),
            (_, _) => Task.FromResult(OkResponse(OneRecordXml())));
        var service = CreateService(handler);

        var result = await service.SearchAsync(new NdlSearchCriteria { ISBN = "9784101010014" });

        Assert.True(result.Succeeded);
        Assert.Single(result.Results);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task SearchAsync_NonTransientStatus_ReturnsFailureWithoutRetry()
    {
        var handler = new SequenceHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));
        var service = CreateService(handler);

        var result = await service.SearchAsync(new NdlSearchCriteria { ISBN = "9784101010014" });

        Assert.False(result.Succeeded);
        Assert.Empty(result.Results);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task SearchAsync_InvalidXml_ReturnsFailureWithoutRetry()
    {
        var handler = new SequenceHandler(
            (_, _) => Task.FromResult(OkResponse("<not-closed")));
        var service = CreateService(handler);

        var result = await service.SearchAsync(new NdlSearchCriteria { ISBN = "9784101010014" });

        Assert.False(result.Succeeded);
        Assert.Empty(result.Results);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task SearchAsync_EmptyResult_ReturnsSuccess()
    {
        var handler = new SequenceHandler(
            (_, _) => Task.FromResult(OkResponse(EmptyResultXml())));
        var service = CreateService(handler);

        var result = await service.SearchAsync(new NdlSearchCriteria { ISBN = "9784101010014" });

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.TotalResults);
        Assert.Empty(result.Results);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public void ParseResponse_MultipleRecords_ExtractsAll()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<searchRetrieveResponse xmlns=""http://www.loc.gov/zing/srw/"">
  <version>1.2</version>
  <numberOfRecords>2</numberOfRecords>
  <records>
    <record>
      <recordData>
        <rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#""
                 xmlns:dcterms=""http://purl.org/dc/terms/""
                 xmlns:foaf=""http://xmlns.com/foaf/0.1/""
                 xmlns:dcndl=""http://ndl.go.jp/dcndl/terms/"">
          <dcndl:BibResource>
            <dcterms:title>本A</dcterms:title>
            <dcterms:identifier rdf:datatype=""http://ndl.go.jp/dcndl/terms/ISBN"">9784000000001</dcterms:identifier>
          </dcndl:BibResource>
        </rdf:RDF>
      </recordData>
      <recordPosition>1</recordPosition>
    </record>
    <record>
      <recordData>
        <rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#""
                 xmlns:dcterms=""http://purl.org/dc/terms/""
                 xmlns:foaf=""http://xmlns.com/foaf/0.1/""
                 xmlns:dcndl=""http://ndl.go.jp/dcndl/terms/"">
          <dcndl:BibResource>
            <dcterms:title>本B</dcterms:title>
            <dcterms:identifier rdf:datatype=""http://ndl.go.jp/dcndl/terms/ISBN"">9784000000002</dcterms:identifier>
          </dcndl:BibResource>
        </rdf:RDF>
      </recordData>
      <recordPosition>2</recordPosition>
    </record>
  </records>
</searchRetrieveResponse>";
        var doc = XDocument.Parse(xml);
        var service = CreateService();

        var result = service.ParseResponse(doc);

        Assert.Equal(2, result.TotalResults);
        Assert.Equal(2, result.Results.Count);
        Assert.Equal("本A", result.Results[0].Title);
        Assert.Equal("本B", result.Results[1].Title);
    }

    [Fact]
    public void BuildCqlQuery_TitleOnly_FormatsCorrectly()
    {
        var query = NdlSearchService.BuildCqlQuery(
            new NdlSearchCriteria { Title = "吾輩は猫である" });
        Assert.Equal("title=\"吾輩は猫である\" AND dpid=\"iss-ndl-opac\"", query);
    }

    [Fact]
    public void BuildCqlQuery_IsbnOnly_FormatsCorrectly()
    {
        var query = NdlSearchService.BuildCqlQuery(
            new NdlSearchCriteria { ISBN = "9784101010014" });

        Assert.Equal(
            "isbn=\"9784101010014\"",
            query);
    }

    [Fact]
    public void BuildCqlQuery_CreatorOnly_OmitsTitle()
    {
        // 著者のみでの検索（タイトル必須でないこと）を検証する。
        var query = NdlSearchService.BuildCqlQuery(
            new NdlSearchCriteria { Creator = "夏目漱石" });
        Assert.Equal("creator=\"夏目漱石\" AND dpid=\"iss-ndl-opac\"", query);
    }

    [Fact]
    public void BuildCqlQuery_PublisherOnly_OmitsOthers()
    {
        var query = NdlSearchService.BuildCqlQuery(
            new NdlSearchCriteria { Publisher = "岩波書店" });
        Assert.Equal("publisher=\"岩波書店\" AND dpid=\"iss-ndl-opac\"", query);
    }

    [Fact]
    public void BuildCqlQuery_AllFields_JoinsWithAnd()
    {
        var query = NdlSearchService.BuildCqlQuery(new NdlSearchCriteria
        {
            Title = "坊っちゃん",
            Creator = "夏目漱石",
            Publisher = "岩波書店",
            YearFrom = 1950,
            YearTo = 2020
        });
        Assert.Equal(
            "title=\"坊っちゃん\" AND creator=\"夏目漱石\" AND publisher=\"岩波書店\""
            + " AND from=\"1950\" AND until=\"2020\" AND dpid=\"iss-ndl-opac\"",
            query);
    }

    [Fact]
    public void BuildCqlQuery_YearRange_UsesFromUntil()
    {
        var query = NdlSearchService.BuildCqlQuery(
            new NdlSearchCriteria { Title = "猫", YearFrom = 2000, YearTo = 2010 });
        Assert.Equal(
            "title=\"猫\" AND from=\"2000\" AND until=\"2010\" AND dpid=\"iss-ndl-opac\"",
            query);
    }

    [Fact]
    public void BuildCqlQuery_PublicationDateDescending_DoesNotSendUnsupportedTitleSort()
    {
        var query = NdlSearchService.BuildCqlQuery(new NdlSearchCriteria
        {
            Title = "猫",
            SortOrder = NdlSearchSortOrder.PublicationDateDescending
        });

        Assert.Equal(
            "title=\"猫\" AND dpid=\"iss-ndl-opac\"",
            query);
    }

    [Fact]
    public void SortAndPageResults_PublicationDateDescending_SortsLocallyAndPaginates()
    {
        var response = new NdlSearchResponse
        {
            TotalResults = 4,
            Results =
            [
                new NdlSearchResult { Title = "不明", PublicationDate = null },
                new NdlSearchResult { Title = "古い", PublicationDate = "1971" },
                new NdlSearchResult { Title = "新しい", PublicationDate = "2024-03" },
                new NdlSearchResult { Title = "中間", PublicationDate = "2001.5" }
            ]
        };

        var result = NdlSearchService.SortAndPageResults(
            response,
            NdlSearchSortOrder.PublicationDateDescending,
            startRecord: 2,
            maxRecords: 2);

        Assert.Equal(4, result.TotalResults);
        Assert.Equal(["中間", "古い"], result.Results.Select(x => x.Title));
    }

    [Fact]
    public void SortAndPageResults_PublicationDateAscending_PutsUnknownDatesLast()
    {
        var response = new NdlSearchResponse
        {
            TotalResults = 3,
            Results =
            [
                new NdlSearchResult { Title = "不明", PublicationDate = "刊行年不明" },
                new NdlSearchResult { Title = "新しい", PublicationDate = "2024" },
                new NdlSearchResult { Title = "古い", PublicationDate = "1971" }
            ]
        };

        var result = NdlSearchService.SortAndPageResults(
            response,
            NdlSearchSortOrder.PublicationDateAscending,
            startRecord: 1,
            maxRecords: 20);

        Assert.Equal(["古い", "新しい", "不明"], result.Results.Select(x => x.Title));
    }

    [Fact]
    public void SortAndPageResults_OverApiLimit_CapsTotalAndMarksTruncated()
    {
        var response = new NdlSearchResponse
        {
            TotalResults = 650,
            Results = Enumerable.Range(1, 500)
                .Select(year => new NdlSearchResult { PublicationDate = year.ToString() })
                .ToList()
        };

        var result = NdlSearchService.SortAndPageResults(
            response,
            NdlSearchSortOrder.PublicationDateDescending,
            startRecord: 1,
            maxRecords: 20);

        Assert.Equal(500, result.TotalResults);
        Assert.True(result.IsTruncated);
        Assert.Equal(20, result.Results.Count);
    }

    [Fact]
    public void BuildCqlQuery_StripsDoubleQuotes()
    {
        var query = NdlSearchService.BuildCqlQuery(
            new NdlSearchCriteria { Title = "引用符\"入り" });
        Assert.Equal("title=\"引用符入り\" AND dpid=\"iss-ndl-opac\"", query);
    }

    [Theory]
    [InlineData(null, null, null, null, true)]
    [InlineData("9784101010014", null, null, null, false)]
    [InlineData(null, "猫", null, null, false)]
    [InlineData(null, null, "漱石", null, false)]
    [InlineData(null, null, null, "岩波", false)]
    public void NdlSearchCriteria_IsEmpty_ReflectsAnyCriteria(
        string? isbn,
        string? title,
        string? creator,
        string? publisher,
        bool expectedEmpty)
    {
        var criteria = new NdlSearchCriteria
        {
            ISBN = isbn,
            Title = title,
            Creator = creator,
            Publisher = publisher
        };
        Assert.Equal(expectedEmpty, criteria.IsEmpty);
    }

    private static HttpResponseMessage OkResponse(string content)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/xml")
        };

    private static string EmptyResultXml() =>
        @"<?xml version=""1.0"" encoding=""UTF-8""?>
<searchRetrieveResponse xmlns=""http://www.loc.gov/zing/srw/"">
  <numberOfRecords>0</numberOfRecords>
  <records/>
</searchRetrieveResponse>";

    private static string OneRecordXml() =>
        @"<?xml version=""1.0"" encoding=""UTF-8""?>
<searchRetrieveResponse xmlns=""http://www.loc.gov/zing/srw/"">
  <numberOfRecords>1</numberOfRecords>
  <records>
    <record>
      <recordData>
        <rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#""
                 xmlns:dcterms=""http://purl.org/dc/terms/""
                 xmlns:dcndl=""http://ndl.go.jp/dcndl/terms/"">
          <dcndl:BibResource>
            <dcterms:title>NDLタイトル</dcterms:title>
            <dcterms:identifier rdf:datatype=""http://ndl.go.jp/dcndl/terms/ISBN"">9784101010014</dcterms:identifier>
          </dcndl:BibResource>
        </rdf:RDF>
      </recordData>
    </record>
  </records>
</searchRetrieveResponse>";

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _responses;

        public SequenceHandler(
            params Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>[] responses)
        {
            _responses = new Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>(
                responses);
        }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return _responses.Dequeue()(request, cancellationToken);
        }
    }
}
