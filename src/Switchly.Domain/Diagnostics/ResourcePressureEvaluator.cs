namespace Switchly.Domain.Diagnostics;

public enum ResourcePressureLevel
{
    Normal = 0,
    Elevated = 1,
    High = 2,
}

public sealed record ResourceSnapshot(
    long AppWorkingSetBytes,
    int SystemMemoryLoadPercent,
    int ActiveProfileCount,
    int SavedProfileCount);

public sealed record ResourceAssessment(
    ResourcePressureLevel Level,
    string StatusSummary,
    string? NonModalWarningMessage);

/// <summary>
/// Pure deterministic evaluator for system/app memory pressure warnings (REQ-018, AC-018, TEST-026).
/// Never auto-closes tabs or discards user forms; emits advisory non-modal warnings only.
/// </summary>
public static class ResourcePressureEvaluator
{
    public static ResourceAssessment Evaluate(ResourceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var appMb = Math.Max(0, snapshot.AppWorkingSetBytes) / (1024.0 * 1024.0);
        var memPct = Math.Clamp(snapshot.SystemMemoryLoadPercent, 0, 100);
        var summary =
            $"Profil tersimpan: {snapshot.SavedProfileCount}  •  Aktif: {snapshot.ActiveProfileCount}  •  RAM Aplikasi: {appMb:F0} MB  •  RAM Sistem: {memPct}%";

        if (snapshot.ActiveProfileCount <= 0)
        {
            return new ResourceAssessment(ResourcePressureLevel.Normal, summary, null);
        }

        if (memPct >= 90 || (memPct >= 85 && snapshot.ActiveProfileCount >= 3))
        {
            return new ResourceAssessment(
                ResourcePressureLevel.High,
                summary,
                $"Penggunaan memori sistem tinggi ({memPct}%, {snapshot.ActiveProfileCount} profil aktif). Tutup sesi profil atau tab yang tidak dipakai agar tetap responsif.");
        }

        if (memPct >= 80 && snapshot.ActiveProfileCount >= 1)
        {
            return new ResourceAssessment(
                ResourcePressureLevel.Elevated,
                summary,
                $"Memori sistem mulai padat ({memPct}%, {snapshot.ActiveProfileCount} profil aktif). Pertimbangkan menutup sesi yang tidak digunakan.");
        }

        return new ResourceAssessment(ResourcePressureLevel.Normal, summary, null);
    }
}
