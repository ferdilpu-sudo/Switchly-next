using System.Text.Json;
using Switchly.Application.Import;
using Switchly.Domain.Import;

namespace Switchly.Infrastructure.Import;

/// <summary>
/// Read-only discovery of local Google Chrome and Microsoft Edge profile directories (TASK-023).
/// Never modifies or locks source browser files.
/// </summary>
public sealed class ChromiumProfileDiscovery : IBrowserProfileDiscovery
{
    public IReadOnlyList<DetectedBrowserProfile> DiscoverLocalProfiles(ImportSourceBrowser? browserFilter = null)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var results = new List<DetectedBrowserProfile>();

        if (!browserFilter.HasValue || browserFilter.Value == ImportSourceBrowser.Chrome)
        {
            var chromeRoot = Path.Combine(localAppData, "Google", "Chrome", "User Data");
            results.AddRange(DiscoverFromUserDataDirectory(ImportSourceBrowser.Chrome, chromeRoot));
        }

        if (!browserFilter.HasValue || browserFilter.Value == ImportSourceBrowser.Edge)
        {
            var edgeRoot = Path.Combine(localAppData, "Microsoft", "Edge", "User Data");
            results.AddRange(DiscoverFromUserDataDirectory(ImportSourceBrowser.Edge, edgeRoot));
        }

        return results;
    }

    public IReadOnlyList<DetectedBrowserProfile> DiscoverFromUserDataDirectory(ImportSourceBrowser browser, string userDataDirectory)
    {
        if (string.IsNullOrWhiteSpace(userDataDirectory) || !Directory.Exists(userDataDirectory))
        {
            return Array.Empty<DetectedBrowserProfile>();
        }

        var friendlyNames = TryReadLocalStateProfileNames(Path.Combine(userDataDirectory, "Local State"));
        var candidateDirs = new List<string>();

        foreach (var dir in Directory.EnumerateDirectories(userDataDirectory))
        {
            var name = Path.GetFileName(dir);
            if (IsCandidateProfileDirectory(name, dir, friendlyNames))
            {
                candidateDirs.Add(dir);
            }
        }

        var results = new List<DetectedBrowserProfile>(candidateDirs.Count);
        foreach (var dir in candidateDirs.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var dirName = Path.GetFileName(dir);
            var hasBookmarks = File.Exists(Path.Combine(dir, "Bookmarks"));
            var hasHistory = File.Exists(Path.Combine(dir, "History"));
            var sessionsDir = Path.Combine(dir, "Sessions");
            var hasSessions = Directory.Exists(sessionsDir) &&
                Directory.EnumerateFiles(sessionsDir).Any();

            var label = friendlyNames.TryGetValue(dirName, out var friendly) && !string.IsNullOrWhiteSpace(friendly)
                ? $"{friendly} ({dirName})"
                : dirName;

            results.Add(new DetectedBrowserProfile(
                browser,
                dirName,
                label,
                dir,
                hasBookmarks,
                hasHistory,
                hasSessions));
        }

        return results;
    }

    private static bool IsCandidateProfileDirectory(
        string directoryName,
        string fullPath,
        IReadOnlyDictionary<string, string> localStateNames)
    {
        if (string.Equals(directoryName, "Default", StringComparison.OrdinalIgnoreCase) ||
            directoryName.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase) ||
            localStateNames.ContainsKey(directoryName))
        {
            return File.Exists(Path.Combine(fullPath, "Bookmarks")) ||
                   File.Exists(Path.Combine(fullPath, "History")) ||
                   File.Exists(Path.Combine(fullPath, "Preferences")) ||
                   Directory.Exists(Path.Combine(fullPath, "Sessions"));
        }

        return false;
    }

    private static Dictionary<string, string> TryReadLocalStateProfileNames(string localStatePath)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(localStatePath))
        {
            return map;
        }

        try
        {
            var info = new FileInfo(localStatePath);
            if (info.Length > ImportLimits.MaxSourceFileBytes)
            {
                return map;
            }

            using var stream = new FileStream(localStatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.TryGetProperty("profile", out var profileObj) &&
                profileObj.TryGetProperty("info_cache", out var infoCache) &&
                infoCache.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in infoCache.EnumerateObject())
                {
                    if (entry.Value.ValueKind == JsonValueKind.Object &&
                        entry.Value.TryGetProperty("name", out var nameProp) &&
                        nameProp.ValueKind == JsonValueKind.String)
                    {
                        var friendly = nameProp.GetString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(friendly))
                        {
                            map[entry.Name] = friendly;
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Non-fatal: fall back to directory names.
        }

        return map;
    }
}
