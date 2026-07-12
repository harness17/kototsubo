using Site.Common;
using Site.Controllers;
using Site.Services;
using Xunit;

namespace Tests;

public class CdImportTests
{
    [Fact]
    public void ToEntity_CdLookupResult_SetsMusicMediaType()
    {
        var entity = CdImportController.ToEntity(new CdLookupResult
        {
            Title = "Album",
            Jan = "9784101010014"
        });

        Assert.Equal(MediaType.Music, entity.MediaType);
    }

    [Fact]
    public void SplitJans_MixedSeparators_ReturnsAllValuesInInputOrder()
    {
        var result = CdImportController.SplitJans(
            "9784101010014, 9784003101018\r\n4901234567894\t9784101010014、4912345678904");

        Assert.Equal(
            ["9784101010014", "9784003101018", "4901234567894", "9784101010014", "4912345678904"],
            result);
    }
}
