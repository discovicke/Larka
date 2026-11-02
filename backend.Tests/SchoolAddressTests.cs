using Backend.Application.Services;

namespace backend.Tests;

public class SchoolAddressTests
{
    [Theory]
    [InlineData("christian.gennari.net25@edu.edugrade.se", "net25")]
    [InlineData("Anna.Svensson.FIB25@skola.se", "fib25")]
    [InlineData("anna.svensson@skola.se", null)]
    [InlineData("anna@skola.se", null)]
    [InlineData("anna.svensson.2@skola.se", null)]
    public void ClassCode_TakesTheLastPartWithLettersAndDigits(string email, string? expected)
    {
        Assert.Equal(expected, SchoolAddress.ClassCode(email));
    }

    [Theory]
    [InlineData(".NET25", "net25")]
    [InlineData("NET 25", "net25")]
    [InlineData("TE22-A", "te22a")]
    public void NormalizeCode_IgnoresCaseAndPunctuation(string text, string expected)
    {
        Assert.Equal(expected, SchoolAddress.NormalizeCode(text));
    }

    [Theory]
    [InlineData("christian.gennari.net25@edu.edugrade.se", "Christian Gennari")]
    [InlineData("christian.gennari@edu.edugrade.se", "Christian Gennari")]
    [InlineData("anna-karin.berg.net25@skola.se", "Anna-Karin Berg")]
    [InlineData("ANNA_SVENSSON.fib25@skola.se", "Anna Svensson")]
    [InlineData("anna.svensson.2@skola.se", "Anna Svensson 2")]
    [InlineData("anna@skola.se", "Anna")]
    [InlineData("kim.van.der.berg.net25@skola.se", "Kim Van Der Berg")]
    public void NameFromEmail_BuildsTheNameFromTheLocalPart(string email, string expected)
    {
        Assert.Equal(expected, SchoolAddress.NameFromEmail(email));
    }

    [Theory]
    [InlineData(".@skola.se")]
    [InlineData("@skola.se")]
    public void NameFromEmail_WithNothingToUse_ReturnsNull(string email)
    {
        Assert.Null(SchoolAddress.NameFromEmail(email));
    }

    [Theory]
    [InlineData("Kim Lind", "kim.lind.net25@skola.se", "Kim Lind")]
    [InlineData(null, "kim.lind.net25@skola.se", "Kim Lind")]
    [InlineData("  ", "kim.lind.net25@skola.se", "Kim Lind")]
    [InlineData("Kim.Lind.NET25@skola.se", "kim.lind.net25@skola.se", "Kim Lind")]
    [InlineData(null, ".@skola.se", ".@skola.se")]
    public void DisplayName_PrefersTheTokenNameUnlessItIsTheEmail(string? tokenName, string email, string expected)
    {
        Assert.Equal(expected, SchoolAddress.DisplayName(tokenName, email));
    }
}
