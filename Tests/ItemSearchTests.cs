using System.ComponentModel.DataAnnotations;
using Dev.CommonLibrary.Common;
using Microsoft.EntityFrameworkCore;
using Site.Common;
using Site.Models;
using Site.Repository;
using Xunit;

namespace Tests;

public class ItemSearchTests
{
    [Fact]
    public void Validate_WhenReleaseDateRangeIsReversed_ReturnsValidationError()
    {
        var model = new ItemSearchViewModel
        {
            ReleaseDateFrom = new DateTime(2026, 6, 2),
            ReleaseDateTo = new DateTime(2026, 6, 1)
        };

        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, x => x.ErrorMessage == "発売日の開始日は終了日以前を指定してください。");
    }

    [Fact]
    public void GetBaseQuery_WithDigitalEdition_AddsTrueDigitalFilter()
    {
        using var context = CreateContext();
        var repository = new ItemRepository(context);
        var cond = new ItemCondModel
        {
            IsDigital = true,
            Publisher = "新潮",
            ReleaseDateFrom = new DateTime(2026, 6, 1),
            ReleaseDateTo = new DateTime(2026, 6, 30),
            Pager = new CommonListPagerModel(1, recoedNumber: 20)
        };

        var sql = repository.GetBaseQuery(cond).ToQueryString();

        Assert.Contains("DECLARE @cond_IsDigital_Value bit = CAST(1 AS bit)", sql);
        Assert.Contains("[i].[IsDigital] = @cond_IsDigital_Value", sql);
        Assert.Contains("[i].[Publisher] LIKE", sql);
        Assert.Contains("[i].[ReleaseDate] >=", sql);
        Assert.Contains("[i].[ReleaseDate] <", sql);
        Assert.Contains("2026-07-01", sql);
    }

    [Fact]
    public void GetBaseQuery_WithPhysicalEdition_AddsFalseDigitalFilter()
    {
        using var context = CreateContext();
        var repository = new ItemRepository(context);
        var cond = new ItemCondModel
        {
            IsDigital = false,
            Pager = new CommonListPagerModel(1, recoedNumber: 20)
        };

        var sql = repository.GetBaseQuery(cond).ToQueryString();

        Assert.Contains("DECLARE @cond_IsDigital_Value bit = CAST(0 AS bit)", sql);
        Assert.Contains("[i].[IsDigital] = @cond_IsDigital_Value", sql);
    }

    [Fact]
    public void GetBaseQuery_WithBothEditions_DoesNotFilterDigitalFlag()
    {
        using var context = CreateContext();
        var repository = new ItemRepository(context);
        var cond = new ItemCondModel
        {
            Pager = new CommonListPagerModel(1, recoedNumber: 20)
        };

        var sql = repository.GetBaseQuery(cond).ToQueryString();

        Assert.DoesNotContain("[i].[IsDigital] =", sql);
    }

    [Fact]
    public void GetBaseQuery_WithMaximumReleaseDate_DoesNotOverflow()
    {
        using var context = CreateContext();
        var repository = new ItemRepository(context);
        var cond = new ItemCondModel
        {
            ReleaseDateTo = DateTime.MaxValue.Date,
            Pager = new CommonListPagerModel(1, recoedNumber: 20)
        };

        var sql = repository.GetBaseQuery(cond).ToQueryString();

        Assert.Contains("[i].[ReleaseDate] <=", sql);
    }

    private static DBContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DBContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=KototsuboQueryTests;Trusted_Connection=True;")
            .Options;

        return new DBContext(options);
    }
}
