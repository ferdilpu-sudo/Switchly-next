using Switchly.Domain.Downloads;

namespace Switchly.Domain.Tests;

public class DownloadFileNameSanitizerTests
{
    [Theory]
    [InlineData("report.pdf", "report.pdf")]
    [InlineData("../../evil/path.exe", "path.exe")]
    [InlineData(@"C:\Windows\System32\cmd.exe", "cmd.exe")]
    [InlineData("file:stream.txt", "file_stream.txt")]
    [InlineData("CON.txt", "_CON.txt")]
    [InlineData("nul", "_nul")]
    [InlineData("trailing.dots...", "trailing.dots")]
    [InlineData("   ", "download.bin")]
    [InlineData(null, "download.bin")]
    public void Sanitize_NeutralizesUnsafeWindowsFilenames(string? input, string expected)
    {
        Assert.Equal(expected, DownloadFileNameSanitizer.Sanitize(input));
    }

    [Fact]
    public void Sanitize_StripsUnicodeRloSpoofingCharacter()
    {
        var spoofed = "invoice_\u202Efdp.exe";
        var sanitized = DownloadFileNameSanitizer.Sanitize(spoofed);
        Assert.DoesNotContain('\u202E', sanitized);
        Assert.Equal("invoice_fdp.exe", sanitized);
    }

    [Fact]
    public void Sanitize_EnforcesMaxLength255()
    {
        var longName = new string('a', 300) + ".png";
        var sanitized = DownloadFileNameSanitizer.Sanitize(longName);
        Assert.Equal(DownloadFileNameSanitizer.MaxLength, sanitized.Length);
        Assert.EndsWith(".png", sanitized, StringComparison.Ordinal);
    }
}
