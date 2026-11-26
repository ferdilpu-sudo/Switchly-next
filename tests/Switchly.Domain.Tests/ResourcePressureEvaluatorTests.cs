using Switchly.Domain.Diagnostics;
using Xunit;

namespace Switchly.Domain.Tests;

public sealed class ResourcePressureEvaluatorTests
{
    [Fact]
    public void Evaluate_NormalMemory_ReturnsNoWarningAndAccurateCounts()
    {
        var snapshot = new ResourceSnapshot(
            AppWorkingSetBytes: 256L * 1024 * 1024,
            SystemMemoryLoadPercent: 52,
            ActiveProfileCount: 1,
            SavedProfileCount: 5);

        var result = ResourcePressureEvaluator.Evaluate(snapshot);

        Assert.Equal(ResourcePressureLevel.Normal, result.Level);
        Assert.Null(result.NonModalWarningMessage);
        Assert.Contains("Profil tersimpan: 5", result.StatusSummary, StringComparison.Ordinal);
        Assert.Contains("Aktif: 1", result.StatusSummary, StringComparison.Ordinal);
        Assert.Contains("RAM Aplikasi: 256 MB", result.StatusSummary, StringComparison.Ordinal);
        Assert.Contains("RAM Sistem: 52%", result.StatusSummary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(80, 1)]
    [InlineData(82, 2)]
    public void Evaluate_ElevatedMemoryWithActiveProfiles_EmitsEarlyNonModalAdvisory(int systemMemPct, int activeProfiles)
    {
        var snapshot = new ResourceSnapshot(
            AppWorkingSetBytes: 900L * 1024 * 1024,
            SystemMemoryLoadPercent: systemMemPct,
            ActiveProfileCount: activeProfiles,
            SavedProfileCount: 4);

        var result = ResourcePressureEvaluator.Evaluate(snapshot);

        Assert.Equal(ResourcePressureLevel.Elevated, result.Level);
        Assert.NotNull(result.NonModalWarningMessage);
    }

    [Theory]
    [InlineData(92, 1)]
    [InlineData(86, 3)]
    public void Evaluate_HighMemoryPressure_EmitsHighNonModalWarningWithoutAutoClosing(int systemMemPct, int activeProfiles)
    {
        var snapshot = new ResourceSnapshot(
            AppWorkingSetBytes: 1800L * 1024 * 1024,
            SystemMemoryLoadPercent: systemMemPct,
            ActiveProfileCount: activeProfiles,
            SavedProfileCount: 6);

        var result = ResourcePressureEvaluator.Evaluate(snapshot);

        Assert.Equal(ResourcePressureLevel.High, result.Level);
        Assert.NotNull(result.NonModalWarningMessage);
        Assert.Contains($"{systemMemPct}%", result.NonModalWarningMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_HighSystemMemoryWithZeroActiveProfiles_ReturnsNoWarning()
    {
        var snapshot = new ResourceSnapshot(
            AppWorkingSetBytes: 380L * 1024 * 1024,
            SystemMemoryLoadPercent: 93,
            ActiveProfileCount: 0,
            SavedProfileCount: 3);

        var result = ResourcePressureEvaluator.Evaluate(snapshot);

        Assert.Equal(ResourcePressureLevel.Normal, result.Level);
        Assert.Null(result.NonModalWarningMessage);
    }
}
