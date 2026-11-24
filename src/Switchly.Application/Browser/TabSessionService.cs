using Switchly.Domain.History;
using Switchly.Domain.Tabs;

namespace Switchly.Application.Browser;

public sealed class TabSessionService : ITabSessionService
{
    private const string AboutBlank = "about:blank";

    private readonly ITabSessionRepository _repository;
    private readonly TimeProvider _timeProvider;

    public TabSessionService(ITabSessionRepository repository, TimeProvider? timeProvider = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task SaveOpenTabsAsync(
        Guid profileId,
        Guid windowId,
        IReadOnlyList<TabSnapshotInput> tabs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tabs);

        var now = _timeProvider.GetUtcNow();
        var entities = new List<BrowserTabSession>();
        var position = 0;

        foreach (var tab in tabs)
        {
            var safeUrl = TrySanitizeTabUrl(tab.Url);
            if (safeUrl is null)
            {
                continue;
            }

            entities.Add(new BrowserTabSession
            {
                TabId = Guid.NewGuid(),
                ProfileId = profileId,
                WindowId = windowId,
                Url = safeUrl,
                Title = string.IsNullOrWhiteSpace(tab.Title) ? null : UrlSanitizer.NormalizeTitle(tab.Title, safeUrl),
                Position = position++,
                IsPinned = tab.IsPinned,
                LastActiveAtUtc = now,
                RestoreState = TabRestoreState.Open,
            });
        }

        await _repository.ReplaceOpenTabsForProfileAsync(profileId, entities, cancellationToken);
    }

    public Task<IReadOnlyList<BrowserTabSession>> GetRestorableTabsAsync(Guid profileId, CancellationToken cancellationToken = default)
        => _repository.ListOpenTabsByProfileAsync(profileId, cancellationToken);

    private static string? TrySanitizeTabUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return null;
        }

        var trimmed = rawUrl.Trim();
        if (string.Equals(trimmed, AboutBlank, StringComparison.OrdinalIgnoreCase))
        {
            return AboutBlank;
        }

        try
        {
            // Persist http/https URLs with credentials & fragments stripped; reject javascript:, file:, data: (schema.md §3 & §5).
            return UrlSanitizer.SanitizeHttpUrl(trimmed, retainQueryString: false);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
