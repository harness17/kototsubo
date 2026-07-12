using Site.Common;
using Site.Controllers;
using Site.Services;
using Xunit;

namespace Tests;

public class GameImportTests
{
    [Fact]
    public void ToEntity_GameLookupResult_SetsGameMediaType()
    {
        var entity = GameImportController.ToEntity(new GameLookupResult
        {
            Title = "Game",
            Jan = "9784101010014"
        });

        Assert.Equal(MediaType.Game, entity.MediaType);
    }

    [Fact]
    public void SplitJans_MixedSeparators_ReturnsAllValuesInInputOrder()
    {
        var result = GameImportController.SplitJans(
            "9784101010014, 9784003101018\r\n4901234567894\t9784101010014、4912345678904");

        Assert.Equal(
            ["9784101010014", "9784003101018", "4901234567894", "9784101010014", "4912345678904"],
            result);
    }
}
