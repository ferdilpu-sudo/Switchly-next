using Switchly.Domain.History;

namespace Switchly.Domain.Tests;

public class UrlSanitizerTests
{
    [Fact]
    public void SanitizeHttpUrl_StripsUserInfoFragmentAndQueryByDefault()
    {
        var result = UrlSanitizer.SanitizeHttpUrl(
            "https://alice:secret@chatgpt.com/c/123?token=abc#section",
            retainQueryString: false);

        Assert.Equal("https://chatgpt.com/c/123", result);
    }

    [Fact]
    public void SanitizeHttpUrl_RetainsQueryWhenRequested_ButStillStripsCredentialsAndFragment()
    {
        var result = UrlSanitizer.SanitizeHttpUrl(
            "https://alice:secret@example.com/search?q=switchly#top",
            retainQueryString: true);

        Assert.Equal("https://example.com/search?q=switchly", result);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("data:text/html,<h1>x</h1>")]
    [InlineData("ftp://example.com")]
    [InlineData("")]
    [InlineData("not-a-url")]
    public void SanitizeHttpUrl_RejectsUnsafeSchemes(string rawUrl)
    {
        Assert.Throws<ArgumentException>(() => UrlSanitizer.SanitizeHttpUrl(rawUrl, retainQueryString: false));
    }
}
