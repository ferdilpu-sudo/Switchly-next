using Switchly.Domain.Import;

namespace Switchly.Application.Import;

/// <summary>Read-only discovery of local Chrome and Edge user profiles (TASK-023).</summary>
public interface IBrowserProfileDiscovery
{
    IReadOnlyList<DetectedBrowserProfile> DiscoverLocalProfiles(ImportSourceBrowser? browserFilter = null);

    IReadOnlyList<DetectedBrowserProfile> DiscoverFromUserDataDirectory(ImportSourceBrowser browser, string userDataDirectory);
}
