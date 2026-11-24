using System.Diagnostics;
using System.Runtime.InteropServices;
using Switchly.Application.Diagnostics;
using Switchly.Domain.Diagnostics;

namespace Switchly.Infrastructure.Diagnostics;

/// <summary>
/// Measures real Windows system memory load (GlobalMemoryStatusEx) and current Switchly + CEF subprocess working set (TASK-034, REQ-018).
/// </summary>
public sealed class WindowsResourceMonitor : IResourceMonitor
{
    public ResourceSnapshot CaptureSnapshot(int savedProfileCount, int activeProfileCount)
    {
        var appBytes = MeasureWorkingSetBytes();
        var sysLoadPct = ReadSystemMemoryLoadPercent();
        return new ResourceSnapshot(appBytes, sysLoadPct, activeProfileCount, savedProfileCount);
    }

    private static long MeasureWorkingSetBytes()
    {
        long total = 0;
        try
        {
            using var current = Process.GetCurrentProcess();
            current.Refresh();
            total += current.WorkingSet64;
        }
        catch
        {
        }

        // Include CEF browser subprocesses spawned by CefSharp.
        try
        {
            foreach (var proc in Process.GetProcessesByName("CefSharp.BrowserSubprocess"))
            {
                try
                {
                    total += proc.WorkingSet64;
                }
                catch
                {
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        catch
        {
        }

        return total;
    }

    private static int ReadSystemMemoryLoadPercent()
    {
        if (!OperatingSystem.IsWindows())
        {
            return 0;
        }

        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        return GlobalMemoryStatusEx(ref status) ? (int)status.dwMemoryLoad : 0;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
