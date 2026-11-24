using Switchly.Domain.Profiles;

namespace Switchly.Domain.Tests;

public class ProfileValidationTests
{
    [Fact]
    public void NormalizeAndValidateName_TrimsValidName()
    {
        var result = ProfileValidation.NormalizeAndValidateName("  Work Account  ");
        Assert.Equal("Work Account", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeAndValidateName_RejectsBlank(string? name)
    {
        Assert.Throws<ArgumentException>(() => ProfileValidation.NormalizeAndValidateName(name));
    }

    [Fact]
    public void NormalizeAndValidateName_RejectsOverMaxLength()
    {
        var tooLong = new string('a', ProfileValidation.MaxNameLength + 1);
        Assert.Throws<ArgumentException>(() => ProfileValidation.NormalizeAndValidateName(tooLong));
    }

    [Theory]
    [InlineData("#1b6ef3", "#1B6EF3")]
    [InlineData("  #aabbcc ", "#AABBCC")]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    public void NormalizeAndValidateColorHex_NormalizesValidInput(string? input, string? expected)
    {
        Assert.Equal(expected, ProfileValidation.NormalizeAndValidateColorHex(input));
    }

    [Theory]
    [InlineData("1B6EF3")]
    [InlineData("#FFF")]
    [InlineData("#GGGGGG")]
    public void NormalizeAndValidateColorHex_RejectsInvalid(string input)
    {
        Assert.Throws<ArgumentException>(() => ProfileValidation.NormalizeAndValidateColorHex(input));
    }

    [Theory]
    [InlineData("https://chatgpt.com", "https://chatgpt.com/")]
    [InlineData("http://localhost:8080/test", "http://localhost:8080/test")]
    [InlineData(null, null)]
    public void NormalizeAndValidateStartupUrl_AcceptsHttpAndHttps(string? input, string? expected)
    {
        Assert.Equal(expected, ProfileValidation.NormalizeAndValidateStartupUrl(input));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("data:text/html,<h1>x</h1>")]
    [InlineData("ftp://example.com")]
    [InlineData("https://user:pass@example.com")]
    [InlineData("not-a-url")]
    public void NormalizeAndValidateStartupUrl_RejectsUnsafeSchemesAndCredentials(string input)
    {
        Assert.Throws<ArgumentException>(() => ProfileValidation.NormalizeAndValidateStartupUrl(input));
    }

    [Fact]
    public void GenerateStorageRelativePath_ProducesSingleSegmentHexGuid()
    {
        var id = Guid.NewGuid();
        var path = ProfileValidation.GenerateStorageRelativePath(id);
        Assert.Equal(32, path.Length);
        Assert.DoesNotContain('/', path);
        Assert.DoesNotContain('\\', path);
        Assert.DoesNotContain("..", path, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateStorageRelativePath_RejectsEmptyGuid()
    {
        Assert.Throws<ArgumentException>(() => ProfileValidation.GenerateStorageRelativePath(Guid.Empty));
    }
}
