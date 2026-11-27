using System.IO;
using CefSharp;
using Switchly.Application.Browser;

namespace Switchly.BrowserHost.BrowserAdapters;

/// <summary>Wraps a per-profile CefSharp RequestContext with direct-child CachePath enforcement (ADR-011).</summary>
internal sealed class CefProfileContext : IBrowserProfileContext
{
    private bool _disposed;

    public CefProfileContext(Guid profileId, string storagePath, string rootCachePath, bool persistSessionCookies)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("ProfileId must not be empty.", nameof(profileId));
        }

        var fullRoot = Path.GetFullPath(rootCachePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullStorage = Path.GetFullPath(storagePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(fullStorage)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!string.Equals(parent, fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Profile CachePath must be a direct child of RootCachePath (ADR-011).",
                nameof(storagePath));
        }

        var leafName = Path.GetFileName(fullStorage);
        if (!(Guid.TryParseExact(leafName, "N", out var leafGuid) || Guid.TryParseExact(leafName, "D", out leafGuid))
            || leafGuid == Guid.Empty)
        {
            throw new ArgumentException(
                "Profile CachePath leaf directory must be a non-empty profile GUID (never CEF reserved 'Default').",
                nameof(storagePath));
        }

        Directory.CreateDirectory(fullStorage);

        var settings = new RequestContextSettings
        {
            CachePath = fullStorage,
            PersistSessionCookies = persistSessionCookies,
        };

        ProfileId = profileId;
        StoragePath = fullStorage;
        RequestContext = new RequestContext(settings);
    }

    public Guid ProfileId { get; }

    public string StoragePath { get; }

    internal RequestContext RequestContext { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        RequestContext.Dispose();
    }
}
