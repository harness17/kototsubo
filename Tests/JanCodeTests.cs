using Site.Common;
using Xunit;

namespace Tests;

public class JanCodeTests
{
    [Fact]
    public void Normalize_ValidJan13_ReturnsSameValue()
    {
        Assert.Equal("9784101010014", JanCode.Normalize("9784101010014"));
    }

    [Fact]
    public void Normalize_HyphensAndWhitespace_ReturnsCompactValue()
    {
        Assert.Equal("9784101010014", JanCode.Normalize(" 978-4-10-101001-4 "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345678")]
    [InlineData("978410101001X")]
    public void Normalize_InvalidValue_ReturnsNull(string? value)
    {
        Assert.Null(JanCode.Normalize(value));
    }

    [Fact]
    public void Normalize_InvalidCheckDigit_ReturnsNull()
    {
        Assert.Null(JanCode.Normalize("9784101010015"));
    }
}
