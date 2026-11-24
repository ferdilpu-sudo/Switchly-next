using System.Buffers.Binary;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Switchly.Application.Browser;
using Switchly.Application.Import;
using Switchly.Domain.Import;
using Switchly.Domain.Profiles;
using Switchly.Infrastructure.Import;
using Switchly.Infrastructure.Persistence;
using Xunit;

namespace Switchly.Infrastructure.Tests;

public sealed class BrowserImportIntegrationTests : IDisposable
{
    private readonly string _tempRoot;

    public BrowserImportIntegrationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"switchly-m006-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Discovery_ReadsLocalStateFriendlyNames_AndDetectsArtifacts()
    {
        var userData = Path.Combine(_tempRoot, "ChromeUserData");
        var defaultProfile = Path.Combine(userData, "Default");
        var workProfile = Path.Combine(userData, "Profile 1");
        Directory.CreateDirectory(Path.Combine(defaultProfile, "Sessions"));
        Directory.CreateDirectory(workProfile);

        File.WriteAllText(
            Path.Combine(userData, "Local State"),
            """
            {
              "profile": {
                "info_cache": {
                  "Default": { "name": "Personal" },
                  "Profile 1": { "name": "Work Account" }
                }
              }
            }
            """);
        File.WriteAllText(Path.Combine(defaultProfile, "Bookmarks"), "{}");
        File.WriteAllText(Path.Combine(defaultProfile, "History"), "");
        File.WriteAllText(Path.Combine(defaultProfile, "Sessions", "Tabs_12345"), "");
        File.WriteAllText(Path.Combine(workProfile, "Preferences"), "{}");

        var discovery = new ChromiumProfileDiscovery();
        var profiles = discovery.DiscoverFromUserDataDirectory(ImportSourceBrowser.Chrome, userData);

        Assert.Equal(2, profiles.Count);
        var p0 = profiles.Single(p => p.ProfileDirectoryName == "Default");
        Assert.Equal("Personal (Default)", p0.DisplayLabel);
        Assert.True(p0.HasBookmarks);
        Assert.True(p0.HasHistory);
        Assert.True(p0.HasSessions);

        var p1 = profiles.Single(p => p.ProfileDirectoryName == "Profile 1");
        Assert.Equal("Work Account (Profile 1)", p1.DisplayLabel);
        Assert.False(p1.HasBookmarks);
    }

    [Fact]
    public void BookmarkImporter_ParsesChromiumJsonAndHtml_AndRejectsUnsafeSchemes()
    {
        var jsonPath = Path.Combine(_tempRoot, "Bookmarks");
        File.WriteAllText(
            jsonPath,
            """
            {
              "roots": {
                "bookmark_bar": {
                  "type": "folder",
                  "name": "Bookmarks Bar",
                  "children": [
                    { "type": "url", "name": "Flow", "url": "https://flow.google/studio" },
                    { "type": "url", "name": "BadJS", "url": "javascript:alert(1)" },
                    { "type": "url", "name": "BadFile", "url": "file:///C:/Windows/win.ini" },
                    { "type": "url", "name": "BadData", "url": "data:text/html,hi" },
                    {
                      "type": "folder",
                      "name": "AI Tools",
                      "children": [
                        { "type": "url", "name": "ChatGPT", "url": "https://chatgpt.com/" }
                      ]
                    }
                  ]
                }
              }
            }
            """);

        var importer = new ChromiumBookmarkImporter();
        var parsed = importer.ParseChromiumBookmarksFile(jsonPath);

        Assert.Equal(2, parsed.TotalUrlCount);
        Assert.Equal(3, parsed.SkippedCount);
        Assert.Contains(parsed.Warnings, w => w.Code == "BookmarksUnsafeUrlsSkipped");

        var htmlPath = Path.Combine(_tempRoot, "bookmarks.html");
        File.WriteAllText(
            htmlPath,
            """
            <DL><p>
              <DT><A HREF="https://dola.com/">Dola</A>
              <DT><A HREF="javascript:void(0)">Unsafe</A>
            </DL>
            """);

        var htmlParsed = importer.ParseNetscapeHtmlFile(htmlPath);
        Assert.Equal(1, htmlParsed.TotalUrlCount);
        Assert.Equal(1, htmlParsed.SkippedCount);
        Assert.Equal("https://dola.com/", htmlParsed.Roots[0].Url);
    }

    [Fact]
    public void HistoryImporter_ParsesValidHistory_StripsQueryStrings_AndHandlesSchemaDifferences()
    {
        var historyPath = Path.Combine(_tempRoot, "History_Valid");
        CreateSyntheticHistoryDb(
            historyPath,
            includeOptionalColumns: true,
            ("https://chatgpt.com/c/123?token=secret123#frag", "ChatGPT Secret", 5, 13370000000000000L),
            ("javascript:alert(1)", "Bad", 1, 13370000000000000L));

        var importer = new ChromiumHistoryImporter();
        var result = importer.ParseChromiumHistoryFile(historyPath);

        Assert.False(result.IsDatabaseLocked);
        Assert.Single(result.Entries);
        Assert.Equal("https://chatgpt.com/c/123", result.Entries[0].Url);
        Assert.DoesNotContain("secret123", result.Entries[0].Url, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, result.SkippedCount);

        // Minimal schema (older/different version with only url column)
        var minimalHistoryPath = Path.Combine(_tempRoot, "History_Minimal");
        CreateSyntheticHistoryDb(
            minimalHistoryPath,
            includeOptionalColumns: false,
            ("https://seaart.ai/", "SeaArt", 1, 0L));

        var minimalResult = importer.ParseChromiumHistoryFile(minimalHistoryPath);
        Assert.False(minimalResult.IsDatabaseLocked);
        Assert.Single(minimalResult.Entries);
        Assert.Equal("https://seaart.ai/", minimalResult.Entries[0].Url);
    }

    [Fact]
    public void HistoryImporter_DetectsLockedDb_MalformedDb_AndUnsupportedSchema()
    {
        var importer = new ChromiumHistoryImporter();

        // 1. Exclusively locked file (simulates open source browser holding exclusive lock, TEST-018)
        var lockedPath = Path.Combine(_tempRoot, "History_Locked");
        File.WriteAllText(lockedPath, "placeholder");
        using (new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var lockedResult = importer.ParseChromiumHistoryFile(lockedPath);
            Assert.True(lockedResult.IsDatabaseLocked);
            Assert.Empty(lockedResult.Entries);
            Assert.Contains(lockedResult.Warnings, w => w.Code == "HistoryDatabaseLocked");
        }

        // 2. Malformed / non-SQLite file
        var malformedPath = Path.Combine(_tempRoot, "History_Malformed");
        File.WriteAllBytes(malformedPath, Encoding.UTF8.GetBytes("not a sqlite database at all"));
        var malformedResult = importer.ParseChromiumHistoryFile(malformedPath);
        Assert.False(malformedResult.IsDatabaseLocked);
        Assert.Empty(malformedResult.Entries);
        Assert.Contains(malformedResult.Warnings, w => w.Code == "HistoryMalformedError");

        // 3. Valid SQLite file but missing 'urls' table (unsupported schema)
        var unsupportedPath = Path.Combine(_tempRoot, "History_UnsupportedSchema");
        using (var conn = new SqliteConnection($"Data Source={unsupportedPath};Pooling=False"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE unrelated (id INTEGER PRIMARY KEY);";
            cmd.ExecuteNonQuery();
        }

        var unsupportedResult = importer.ParseChromiumHistoryFile(unsupportedPath);
        Assert.False(unsupportedResult.IsDatabaseLocked);
        Assert.Empty(unsupportedResult.Entries);
        Assert.Contains(unsupportedResult.Warnings, w => w.Code == "HistorySchemaUnsupported");
    }

    [Fact]
    public void OpenTabImporter_ParsesValidSnss_AndReportsUnsupportedSnssVersion()
    {
        var importer = new ChromiumOpenTabImporter();

        // Valid SNSS v3 file with two safe URLs and one unsafe javascript: URL
        var validProfileDir = Path.Combine(_tempRoot, "SnssValidProfile");
        var sessionsDir = Path.Combine(validProfileDir, "Sessions");
        Directory.CreateDirectory(sessionsDir);
        WriteSyntheticSnssFile(
            Path.Combine(sessionsDir, "Tabs_1000"),
            version: 3,
            ("https://flow.google/projects?secret=abc", "Google Flow"),
            ("https://dola.com/", "Dola"),
            ("javascript:alert(1)", "Evil"));

        var parsed = importer.ParseChromiumSessionsDirectory(validProfileDir);
        Assert.Equal(2, parsed.Tabs.Count);
        Assert.Contains(parsed.Tabs, t => t.Url == "https://flow.google/projects");
        Assert.Contains(parsed.Tabs, t => t.Url == "https://dola.com/");
        Assert.Equal(1, parsed.SkippedCount);

        // Unsupported SNSS version (v99)
        var unsupportedProfileDir = Path.Combine(_tempRoot, "SnssUnsupportedProfile");
        var unsupportedSessions = Path.Combine(unsupportedProfileDir, "Sessions");
        Directory.CreateDirectory(unsupportedSessions);
        WriteSyntheticSnssFile(Path.Combine(unsupportedSessions, "Tabs_2000"), version: 99);

        var unsupportedParsed = importer.ParseChromiumSessionsDirectory(unsupportedProfileDir);
        Assert.Empty(unsupportedParsed.Tabs);
        Assert.Contains(unsupportedParsed.Warnings, w => w.Code == "OpenTabsUnsupportedFormat");
    }

    [Fact]
    public async Task BrowserImportService_PreviewExecute_SkipsDuplicates_RedactsAudit_AndPreservesJobOnProfileDelete()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();

        var options = new DbContextOptionsBuilder<SwitchlyDbContext>()
            .UseSqlite(conn)
            .Options;

        await using var db = new SwitchlyDbContext(options);
        await db.Database.MigrateAsync();

        var profileId = Guid.NewGuid();
        db.Profiles.Add(new BrowserProfile
        {
            ProfileId = profileId,
            Name = "Target Profile",
            StorageRelativePath = profileId.ToString("D"),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        // Build synthetic Chrome profile directory (including a Cookies file that MUST NEVER be read/imported)
        var sourceDir = Path.Combine(_tempRoot, "SyntheticChromeProfile");
        Directory.CreateDirectory(Path.Combine(sourceDir, "Sessions"));
        File.WriteAllText(
            Path.Combine(sourceDir, "Bookmarks"),
            """
            {
              "roots": {
                "bookmark_bar": {
                  "type": "folder",
                  "name": "Bar",
                  "children": [
                    { "type": "url", "name": "Flow", "url": "https://flow.google/" },
                    { "type": "url", "name": "SeaArt", "url": "https://seaart.ai/" }
                  ]
                }
              }
            }
            """);
        CreateSyntheticHistoryDb(
            Path.Combine(sourceDir, "History"),
            includeOptionalColumns: true,
            ("https://chatgpt.com/?auth=DO_NOT_LEAK_TOKEN", "ChatGPT", 3, 13370000000000000L));
        WriteSyntheticSnssFile(
            Path.Combine(sourceDir, "Sessions", "Tabs_999"),
            version: 3,
            ("https://dola.com/", "Dola"));
        File.WriteAllText(Path.Combine(sourceDir, "Cookies"), "SENSITIVE_COOKIE_VALUE");

        var detected = new DetectedBrowserProfile(
            ImportSourceBrowser.Chrome,
            "Default",
            "Personal (Default)",
            sourceDir,
            HasBookmarks: true,
            HasHistory: true,
            HasSessions: true);

        var bookmarkService = new BookmarkService(new EfBookmarkRepository(db));
        var historyService = new HistoryService(new EfHistoryRepository(db));
        var tabSessionService = new TabSessionService(new EfTabSessionRepository(db));
        var importJobRepo = new EfImportJobRepository(db);
        var service = new BrowserImportService(
            new ChromiumProfileDiscovery(),
            new ChromiumBookmarkImporter(),
            new ChromiumHistoryImporter(),
            new ChromiumOpenTabImporter(),
            bookmarkService,
            historyService,
            tabSessionService,
            importJobRepo);

        var allCategories = ImportCategories.Bookmarks | ImportCategories.History | ImportCategories.OpenTabs;

        // 1. Preview before execute
        var preview = await service.PreviewAsync(detected, allCategories, profileId);
        Assert.Equal(2, preview.BookmarkCount);
        Assert.Equal(0, preview.BookmarkDuplicateCount);
        Assert.Equal(1, preview.HistoryCount);
        Assert.Equal(1, preview.OpenTabCount);
        Assert.False(preview.IsHistoryDatabaseLocked);

        // 2. Execute import
        var exec = await service.ExecuteAsync(detected, allCategories, profileId);
        Assert.Equal(ImportJobStatus.Completed, exec.Status);
        Assert.Equal(2, exec.BookmarksImported);
        Assert.Equal(1, exec.HistoryImported);
        Assert.Equal(1, exec.OpenTabsImported);

        // 3. Second preview sees duplicates accurately
        var preview2 = await service.PreviewAsync(detected, allCategories, profileId);
        Assert.Equal(2, preview2.BookmarkDuplicateCount);
        Assert.Equal(1, preview2.HistoryDuplicateCount);

        // 4. Verify ImportJob audit record contains no raw secrets/query tokens/cookie values (TEST-022)
        var savedJob = await importJobRepo.GetByIdAsync(exec.ImportJobId);
        Assert.NotNull(savedJob);
        Assert.Equal(ImportJobStatus.Completed, savedJob!.Status);
        Assert.NotNull(savedJob.CompletedAtUtc);
        Assert.DoesNotContain("DO_NOT_LEAK_TOKEN", savedJob.ResultSummaryJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SENSITIVE_COOKIE_VALUE", savedJob.ResultSummaryJson, StringComparison.OrdinalIgnoreCase);

        // 5. Deleting target profile sets ImportJob.TargetProfileId = NULL (ON DELETE SET NULL, ENT-012)
        var profile = await db.Profiles.FindAsync(profileId);
        db.Profiles.Remove(profile!);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var retainedJob = await importJobRepo.GetByIdAsync(exec.ImportJobId);
        Assert.NotNull(retainedJob);
        Assert.Null(retainedJob!.TargetProfileId);
    }

    private static void CreateSyntheticHistoryDb(
        string path,
        bool includeOptionalColumns,
        params (string Url, string Title, int VisitCount, long LastVisitTime)[] rows)
    {
        using var conn = new SqliteConnection($"Data Source={path};Pooling=False");
        conn.Open();

        using var create = conn.CreateCommand();
        create.CommandText = includeOptionalColumns
            ? "CREATE TABLE urls (id INTEGER PRIMARY KEY, url LONGVARCHAR, title LONGVARCHAR, visit_count INTEGER, last_visit_time INTEGER);"
            : "CREATE TABLE urls (id INTEGER PRIMARY KEY, url LONGVARCHAR);";
        create.ExecuteNonQuery();

        foreach (var row in rows)
        {
            using var insert = conn.CreateCommand();
            if (includeOptionalColumns)
            {
                insert.CommandText = "INSERT INTO urls (url, title, visit_count, last_visit_time) VALUES ($u, $t, $v, $l);";
                insert.Parameters.AddWithValue("$u", row.Url);
                insert.Parameters.AddWithValue("$t", row.Title);
                insert.Parameters.AddWithValue("$v", row.VisitCount);
                insert.Parameters.AddWithValue("$l", row.LastVisitTime);
            }
            else
            {
                insert.CommandText = "INSERT INTO urls (url) VALUES ($u);";
                insert.Parameters.AddWithValue("$u", row.Url);
            }

            insert.ExecuteNonQuery();
        }
    }

    private static void WriteSyntheticSnssFile(
        string path,
        uint version,
        params (string Url, string Title)[] navigations)
    {
        using var ms = new MemoryStream();
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(header[..4], 0x53534E53); // "SNSS"
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..8], version);
        ms.Write(header);

        Span<byte> intBuf = stackalloc byte[4];
        Span<byte> sizeBuf = stackalloc byte[2];
        var tabId = 1;
        foreach (var (url, title) in navigations)
        {
            var urlBytes = Encoding.UTF8.GetBytes(url);
            var urlPad = ((urlBytes.Length + 3) & ~3) - urlBytes.Length;
            var titleBytes = Encoding.Unicode.GetBytes(title);
            var titlePad = ((titleBytes.Length + 3) & ~3) - titleBytes.Length;

            using var pickle = new MemoryStream();
            var picklePayloadSize = 4 + 4 + 4 + urlBytes.Length + urlPad + 4 + titleBytes.Length + titlePad;
            BinaryPrimitives.WriteInt32LittleEndian(intBuf, picklePayloadSize);
            pickle.Write(intBuf);
            BinaryPrimitives.WriteInt32LittleEndian(intBuf, tabId++);
            pickle.Write(intBuf);
            BinaryPrimitives.WriteInt32LittleEndian(intBuf, 0);
            pickle.Write(intBuf);
            BinaryPrimitives.WriteInt32LittleEndian(intBuf, urlBytes.Length);
            pickle.Write(intBuf);
            pickle.Write(urlBytes);
            if (urlPad > 0)
            {
                pickle.Write(new byte[urlPad]);
            }

            BinaryPrimitives.WriteInt32LittleEndian(intBuf, title.Length);
            pickle.Write(intBuf);
            pickle.Write(titleBytes);
            if (titlePad > 0)
            {
                pickle.Write(new byte[titlePad]);
            }

            var payloadBytes = pickle.ToArray();
            var cmdSize = (ushort)(1 + payloadBytes.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(sizeBuf, cmdSize);
            ms.Write(sizeBuf);
            ms.WriteByte(6); // kCommandUpdateTabNavigationSession
            ms.Write(payloadBytes);
        }

        File.WriteAllBytes(path, ms.ToArray());
    }
}
