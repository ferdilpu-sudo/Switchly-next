using Switchly.Domain.Browser;

namespace Switchly.Domain.Tests;

public sealed class BrowserUrlPolicyTests
{
    [Theory]
    [InlineData("https://chatgpt.com", true)]
    [InlineData("http://localhost:3000/test", true)]
    [InlineData("about:blank", true)]
    [InlineData("file:///C:/Windows/win.ini", false)]
    [InlineData("chrome://settings", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,<script>alert(1)</script>", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsAllowedNavigationUrl_EnforcesSafeSchemeWhitelist(string url, bool expected)
    {
        Assert.Equal(expected, BrowserUrlPolicy.IsAllowedNavigationUrl(url));
    }

    [Theory]
    [InlineData("https://flow.google", "https://flow.google/")]
    [InlineData("chatgpt.com", "https://chatgpt.com/")]
    [InlineData("localhost:5000", "http://localhost:5000/")]
    [InlineData("about:blank", "about:blank")]
    public void ResolveAddressBarInput_ResolvesValidUrls(string input, string expected)
    {
        Assert.Equal(expected, BrowserUrlPolicy.ResolveAddressBarInput(input));
    }

    [Theory]
    [InlineData("file:///C:/secret.txt")]
    [InlineData("chrome://settings")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hi")]
    public void ResolveAddressBarInput_BlocksDangerousSchemes(string input)
    {
        Assert.Null(BrowserUrlPolicy.ResolveAddressBarInput(input));
    }

    [Fact]
    public void ResolveAddressBarInput_ConvertsPlainTextToSearchUrl()
    {
        var resolved = BrowserUrlPolicy.ResolveAddressBarInput("cara pakai switchly");
        Assert.Equal("https://www.google.com/search?q=cara%20pakai%20switchly", resolved);
    }
}
