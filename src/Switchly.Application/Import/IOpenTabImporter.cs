namespace Switchly.Application.Import;

/// <summary>Read-only bounded parser for Chromium SNSS open-tab session files (TASK-026).</summary>
public interface IOpenTabImporter
{
    OpenTabsParseResult ParseChromiumSessionsDirectory(string profileDirectoryPath);
}
