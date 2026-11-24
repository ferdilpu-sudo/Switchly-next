using System.Security.Cryptography;
using System.Text;
using Switchly.Application.Profiles;

namespace Switchly.Infrastructure.Persistence;

/// <summary>
/// Kernel-managed Windows named mutex guard scoped to RootCachePath (ADR-011, TEST-017).
/// Automatically released by the OS on process exit or crash (no stale PID lock files).
/// </summary>
public sealed class SingleInstanceMutexGuard : IAppInstanceGuard
{
    private readonly string _mutexName;
    private Mutex? _mutex;
    private bool _ownsMutex;
    private bool _disposed;

    public SingleInstanceMutexGuard(string rootCachePath)
    {
        if (string.IsNullOrWhiteSpace(rootCachePath))
        {
            throw new ArgumentException("RootCachePath is required.", nameof(rootCachePath));
        }

        var normalized = Path.GetFullPath(rootCachePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..16];
        _mutexName = $"Local\\SwitchlyNext.Instance.{hash}";
    }

    public bool TryAcquire()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_ownsMutex)
        {
            return true;
        }

        var mutex = new Mutex(initiallyOwned: true, _mutexName, out var createdNew);
        if (createdNew)
        {
            _mutex = mutex;
            _ownsMutex = true;
            return true;
        }

        mutex.Dispose();
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_mutex is not null)
        {
            if (_ownsMutex)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // Ignore if released from a different shutdown thread.
                }
            }

            _mutex.Dispose();
            _mutex = null;
            _ownsMutex = false;
        }
    }
}
