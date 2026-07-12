using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Dev.CommonLibrary.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Site.Common;
using Site.Controllers;
using Site.Models;
using Site.Repository;
using Xunit;

namespace Tests;

public class WordTests
{
    [Fact]
    public void WordViewModel_WhenBodyIsEmpty_ReturnsValidationError()
    {
        var model = new WordViewModel();
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(WordViewModel.Body)));
    }

    [Fact]
    public void WordViewModel_UserIdCannotBeBoundFromRequest()
    {
        var attribute = typeof(WordViewModel)
            .GetProperty(nameof(WordViewModel.UserId))!
            .GetCustomAttribute<BindNeverAttribute>();

        Assert.NotNull(attribute);
    }

    [Fact]
    public void WordRepository_WithSearchConditions_AddsAllFilters()
    {
        using var context = CreateContext();
        var repository = new WordRepository(context);
        var cond = new WordCondModel
        {
            Keyword = "言葉",
            Genre = WordGenre.Book,
            ItemId = 42,
            Pager = new CommonListPagerModel(1, recoedNumber: 20)
        };

        var sql = repository.GetBaseQuery(cond).ToQueryString();

        Assert.Contains("[w].[DelFlag] = CAST(0 AS bit)", sql);
        Assert.Contains("[w].[Body] LIKE", sql);
        Assert.Contains("[w].[Genre] =", sql);
        Assert.Contains("[w].[ItemId] =", sql);
    }

    [Fact]
    public void WordController_RequiresAuthorization()
    {
        Assert.NotNull(Attribute.GetCustomAttribute(
            typeof(WordController),
            typeof(AuthorizeAttribute)));
    }

    [Fact]
    public void WordController_SearchItems_IsAuthorizedGetAction()
    {
        var method = typeof(WordController).GetMethod(
            nameof(WordController.SearchItems),
            new[] { typeof(string) });

        Assert.NotNull(method);
        Assert.NotEmpty(method.GetCustomAttributes(
            typeof(AuthorizeAttribute),
            true));
        Assert.NotEmpty(method.GetCustomAttributes(
            typeof(HttpGetAttribute),
            true));
    }

    [Fact]
    public void WordController_SearchItems_WithBlankKeyword_ReturnsEmptyJson()
    {
        var controller = new WordController(null!, null!, null!);

        var result = Assert.IsType<JsonResult>(controller.SearchItems(" "));
        var items = Assert.IsAssignableFrom<IEnumerable<object>>(result.Value);

        Assert.Empty(items);
    }

    [Fact]
    public void ItemRepository_SearchForWordAssociation_FiltersActiveItemsAndOrdersResults()
    {
        using var context = CreateContext();
        var repository = new ItemRepository(context);

        var sql = repository.SearchForWordAssociation("ジョジョ").ToQueryString();

        Assert.Contains("DECLARE @p int = 20", sql);
        Assert.Contains("TOP(@p)", sql);
        Assert.Contains("[i].[DelFlag] = CAST(0 AS bit)", sql);
        Assert.Contains("[i].[Title] LIKE", sql);
        Assert.Contains("[i].[Creator] LIKE", sql);
        Assert.Contains("ORDER BY [i].[Title], [i].[Creator]", sql);
    }

    [Theory]
    [InlineData(nameof(WordController.Create))]
    [InlineData(nameof(WordController.Edit))]
    [InlineData(nameof(WordController.DeleteConfirmed))]
    public void WordPostActions_ValidateAntiforgeryToken(string actionName)
    {
        var method = typeof(WordController)
            .GetMethods()
            .Single(candidate =>
                candidate.Name == actionName &&
                candidate.GetCustomAttributes(typeof(HttpPostAttribute), true).Length > 0);

        Assert.NotEmpty(method.GetCustomAttributes(
            typeof(ValidateAntiForgeryTokenAttribute),
            true));
    }

    private static DBContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DBContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=KototsuboWordQueryTests;Trusted_Connection=True;")
            .Options;

        return new DBContext(options);
    }
}
