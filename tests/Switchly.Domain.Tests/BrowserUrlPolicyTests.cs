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

    [Theory]
    [InlineData("", true)]
    [InlineData("about:blank", true)]
    [InlineData("https://accounts.google.com/o/oauth2/auth", true)]
    [InlineData("file:///C:/Windows/win.ini", false)]
    [InlineData("chrome://settings", false)]
    [InlineData("javascript:alert(1)", false)]
    public void IsAllowedPopupUrl_EnforcesSafePopupSchemes(string url, bool expected)
    {
        Assert.Equal(expected, BrowserUrlPolicy.IsAllowedPopupUrl(url));
    }

    [Theory]
    [InlineData("https://example.com/file.pdf", true)]
    [InlineData("blob:https://flow.google.com/1234", true)]
    [InlineData("data:text/plain;base64,SGk=", true)]
    [InlineData("file:///C:/secret.txt", false)]
    [InlineData("chrome://settings", false)]
    public void IsAllowedDownloadUrl_BlocksLocalFileDownloads(string url, bool expected)
    {
        Assert.Equal(expected, BrowserUrlPolicy.IsAllowedDownloadUrl(url));
    }
}
