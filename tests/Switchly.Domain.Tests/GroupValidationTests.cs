using Switchly.Domain.Groups;

namespace Switchly.Domain.Tests;

public class GroupValidationTests
{
    [Fact]
    public void NormalizeAndValidateName_TrimsValidName()
    {
        Assert.Equal("AI Tools", GroupValidation.NormalizeAndValidateName("  AI Tools  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeAndValidateName_RejectsBlank(string? name)
    {
        Assert.Throws<ArgumentException>(() => GroupValidation.NormalizeAndValidateName(name));
    }

    [Theory]
    [InlineData("https://CHATGPT.COM/c/123", "chatgpt.com")]
    [InlineData("https://www.seaart.ai/", "seaart.ai")]
    [InlineData("https://seaart.ai/explore", "seaart.ai")]
    [InlineData("https://www.dola.com/chat/", "dola.com")]
    [InlineData("https://flow.google.com/about", "flow.google.com")]
    [InlineData("www.example.com", "example.com")]
    public void NormalizeDomainKey_NormalizesAndStripsWww(string input, string expected)
    {
        Assert.Equal(expected, GroupValidation.NormalizeDomainKey(input));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/test.html")]
    [InlineData("data:text/html,hello")]
    [InlineData("ftp://example.com")]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeDomainKey_RejectsUnsafeOrInvalid(string input)
    {
        Assert.Throws<ArgumentException>(() => GroupValidation.NormalizeDomainKey(input));
    }
}
