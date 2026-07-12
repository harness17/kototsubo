using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Site.Common;
using Site.Controllers;
using Site.Entity;
using Site.Repository;
using Site.Services;
using Xunit;

namespace Tests;

public class ItemControllerTests
{
    [Fact]
    public async Task LookupAsin_NdlLookupFailed_ReturnsRetryableMessage()
    {
        var candidateService = new Mock<IBookCandidateLookupService>();
        candidateService
            .Setup(x => x.LookupCandidatesByIsbnAsync("9784101010014"))
            .ReturnsAsync(new BookLookupCandidates
            {
                Results = [],
                NdlLookupFailed = true
            });
        var controller = CreateController(candidateService.Object);

        var result = await controller.LookupAsin("4101010013");

        dynamic json = Assert.IsType<JsonResult>(result).Value!;
        Assert.False((bool)json.success);
        Assert.Equal(
            "書誌情報の取得に失敗しました。しばらくしてから再度お試しください。",
            (string)json.message);
    }

    [Fact]
    public async Task LookupAsin_NdlLookupSucceededWithNoMatch_ReturnsNotFoundMessage()
    {
        var candidateService = new Mock<IBookCandidateLookupService>();
        candidateService
            .Setup(x => x.LookupCandidatesByIsbnAsync("9784101010014"))
            .ReturnsAsync(new BookLookupCandidates
            {
                Results = [],
                NdlLookupFailed = false
            });
        var controller = CreateController(candidateService.Object);

        var result = await controller.LookupAsin("4101010013");

        dynamic json = Assert.IsType<JsonResult>(result).Value!;
        Assert.False((bool)json.success);
        Assert.Equal("該当する書籍が見つかりませんでした", (string)json.message);
    }

    [Fact]
    public async Task LookupAsin_NdlLookupSucceededWithMatch_ReturnsBook()
    {
        var candidateService = new Mock<IBookCandidateLookupService>();
        candidateService
            .Setup(x => x.LookupCandidatesByIsbnAsync("9784101010014"))
            .ReturnsAsync(new BookLookupCandidates
            {
                Results = [new BookLookupResult { ISBN = "9784101010014", Title = "吾輩は猫である" }],
                NdlLookupFailed = false
            });
        var controller = CreateController(candidateService.Object);

        var result = await controller.LookupAsin("4101010013");

        dynamic json = Assert.IsType<JsonResult>(result).Value!;
        Assert.True((bool)json.success);
        Assert.Equal("吾輩は猫である", (string)json.data.Title);
    }

    private static ItemController CreateController(
        IBookCandidateLookupService candidateLookupService)
    {
        var options = new DbContextOptionsBuilder<DBContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=KototsuboItemControllerTests;Trusted_Connection=True;")
            .Options;
        var repository = new Mock<ItemRepository>(new DBContext(options));

        return new ItemController(
            repository.Object,
            Mock.Of<IMapper>(),
            candidateLookupService);
    }
}
