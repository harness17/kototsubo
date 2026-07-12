using Site.Common;
using Site.Controllers;
using Site.Services;
using Xunit;

namespace Tests;

public class DvdImportTests
{
    [Fact]
    public void ToEntity_DvdLookupResult_MapsMovieFields()
    {
        var entity = DvdImportController.ToEntity(new DvdLookupResult
        {
            Title = "Movie",
            Creator = "Director",
            Publisher = "Label",
            Jan = "9784101010014",
            Format = "Blu-ray",
            DiscCount = 2
        });

        Assert.Equal(MediaType.Movie, entity.MediaType);
        Assert.Equal("Director", entity.Creator);
        Assert.Equal("Label", entity.Publisher);
        Assert.Equal("Blu-ray", entity.Format);
        Assert.Equal(2, entity.DiscCount);
    }

    [Fact]
    public void SplitJans_MixedSeparators_ReturnsAllValuesInInputOrder()
    {
        var result = DvdImportController.SplitJans(
            "9784101010014, 9784003101018\r\n4901234567894\t9784101010014、4912345678904");

        Assert.Equal(
            ["9784101010014", "9784003101018", "4901234567894", "9784101010014", "4912345678904"],
            result);
    }
}
