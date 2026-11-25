using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using Switchly.App.Common;
using Switchly.Application.Browser;
using Switchly.Domain.Downloads;

namespace Switchly.App.ViewModels;

/// <summary>Display projection for native-browser download flyout and library items.</summary>
public sealed class DownloadItemViewModel : ViewModelBase
{
    private string _fileName;
    private string _targetPath;
    private DownloadState _state;
    private long _receivedBytes;
    private long? _totalBytes;

    public DownloadItemViewModel(
        string fileName,
        string targetPath,
        DownloadState state,
        long receivedBytes,
        long? totalBytes)
    {
        _fileName = fileName;
        _targetPath = targetPath;
        _state = state;
        _receivedBytes = receivedBytes;
        _totalBytes = totalBytes;
    }

    public static DownloadItemViewModel FromRecord(DownloadRecord record)
        => new(record.SuggestedFileName, record.TargetPath, record.State, record.BytesReceived ?? 0, record.TotalBytes);

    public static DownloadItemViewModel FromEvent(DownloadUpdateEvent evt)
        => new(evt.SuggestedFileName, evt.TargetPath, evt.State, evt.BytesReceived, evt.TotalBytes);

    public string FileName
    {
        get => _fileName;
        private set => SetProperty(ref _fileName, value);
    }

    public string TargetPath
    {
        get => _targetPath;
        private set => SetProperty(ref _targetPath, value);
    }

    public DownloadState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                NotifyDerivedProperties();
            }
        }
    }

    public long ReceivedBytes
    {
        get => _receivedBytes;
        private set
        {
            if (SetProperty(ref _receivedBytes, value))
            {
                NotifyDerivedProperties();
            }
        }
    }

    public long? TotalBytes
    {
        get => _totalBytes;
        private set
        {
            if (SetProperty(ref _totalBytes, value))
            {
                NotifyDerivedProperties();
            }
        }
    }

    public bool IsInProgress => State == DownloadState.InProgress;

    public bool IsCompleted => State == DownloadState.Completed;

    public bool IsIndeterminate => IsInProgress && (!TotalBytes.HasValue || TotalBytes.Value <= 0);

    public double ProgressPercent
    {
        get
        {
            if (State == DownloadState.Completed)
            {
                return 100.0;
            }

            if (!TotalBytes.HasValue || TotalBytes.Value <= 0)
            {
                return 0.0;
            }

            return Math.Clamp((double)ReceivedBytes / TotalBytes.Value * 100.0, 0.0, 100.0);
        }
    }

    public string StatusColor => State switch
    {
        DownloadState.Completed => "#22C55E",
        DownloadState.InProgress => "#3B82F6",
        DownloadState.Cancelled => "#F59E0B",
        _ => "#EF4444",
    };

    public string StatusSummary => State switch
    {
        DownloadState.InProgress when TotalBytes is > 0 =>
            $"Mengunduh  •  {FormatBytes(ReceivedBytes)} / {FormatBytes(TotalBytes.Value)} ({ProgressPercent:0}%)",
        DownloadState.InProgress =>
            $"Mengunduh  •  {FormatBytes(ReceivedBytes)}",
        DownloadState.Completed =>
            $"Selesai  •  {FormatBytes(TotalBytes is > 0 ? TotalBytes.Value : ReceivedBytes)}",
        DownloadState.Cancelled => "Dibatalkan",
        _ => "Gagal",
    };

    public void ApplyUpdate(DownloadUpdateEvent evt)
    {
        FileName = evt.SuggestedFileName;
        TargetPath = evt.TargetPath;
        ReceivedBytes = evt.BytesReceived;
        TotalBytes = evt.TotalBytes;
        State = evt.State;
    }

    public static void OpenDownloadedFile(string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath))
        {
            MessageBox.Show("File unduhan sudah tidak ditemukan di disk.", "Unduhan — Switchly", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(targetPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Tidak dapat membuka file: {ex.Message}", "Unduhan — Switchly", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public static void ShowInExplorer(string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return;
        }

        if (File.Exists(targetPath))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{targetPath}\"") { UseShellExecute = true });
            return;
        }

        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
        else
        {
            MessageBox.Show("File atau folder unduhan sudah tidak ditemukan di disk.", "Unduhan — Switchly", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void NotifyDerivedProperties()
    {
        OnPropertyChanged(nameof(IsInProgress));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(IsIndeterminate));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(StatusSummary));
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        double kb = bytes / 1024.0;
        if (kb < 1024)
        {
            return $"{kb.ToString("0.#", CultureInfo.InvariantCulture)} KB";
        }

        double mb = kb / 1024.0;
        if (mb < 1024)
        {
            return $"{mb.ToString("0.#", CultureInfo.InvariantCulture)} MB";
        }

        double gb = mb / 1024.0;
        return $"{gb.ToString("0.##", CultureInfo.InvariantCulture)} GB";
    }
}
