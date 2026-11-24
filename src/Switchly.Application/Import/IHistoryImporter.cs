namespace Switchly.Application.Import;

/// <summary>Read-only parser for Chromium SQLite History databases with lock & schema handling (TASK-025).</summary>
public interface IHistoryImporter
{
    HistoryParseResult ParseChromiumHistoryFile(string historyDatabasePath);
}
