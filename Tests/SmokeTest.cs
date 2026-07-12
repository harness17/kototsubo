using Dev.CommonLibrary.Entity;
using Site.Common;
using Xunit;

namespace Tests;

public class SmokeTest
{
    [Fact]
    public void EntityBase_AuditColumns_Exist()
    {
        var entity = new TestEntity();
        Assert.NotNull(entity);
        Assert.False(entity.DelFlag);
    }

    [Fact]
    public void ApplicationRoleType_HasExpectedValues()
    {
        Assert.Equal(1, (int)ApplicationRoleType.Admin);
        Assert.Equal(2, (int)ApplicationRoleType.Member);
    }

    [Fact]
    public void ErrorViewModel_DefaultValues()
    {
        var model = new Site.Models.ErrorViewModel();
        Assert.Null(model.RequestId);
        Assert.False(model.ShowRequestId);
    }

    private class TestEntity : SiteEntityBase
    {
    }
}
