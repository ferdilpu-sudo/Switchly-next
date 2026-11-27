using Switchly.Domain.History;

namespace Switchly.Application.Browser;

public sealed class HistoryService : IHistoryService
{
    private readonly IHistoryRepository _repository;
    private readonly TimeProvider _timeProvider;

    public HistoryService(IHistoryRepository repository, TimeProvider? timeProvider = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<HistoryEntry> RecordVisitAsync(
        Guid profileId,
        string url,
        string? title = null,
        HistoryTransitionKind transitionKind = HistoryTransitionKind.Link,
        bool retainQueryString = false,
        CancellationToken cancellationToken = default)
    {
        var sanitizedUrl = UrlSanitizer.SanitizeHttpUrl(url, retainQueryString);
        var meaningfulTitle = IsPlaceholderTitle(title) ? null : title;
        var now = _timeProvider.GetUtcNow();
        var existing = await _repository.FindByProfileAndUrlAsync(profileId, sanitizedUrl, cancellationToken);

        if (existing is not null)
        {
            existing.VisitCount += 1;
            existing.VisitedAtUtc = now;
            existing.TransitionKind = transitionKind;
            if (!string.IsNullOrWhiteSpace(meaningfulTitle))
            {
                existing.Title = UrlSanitizer.NormalizeTitle(meaningfulTitle, sanitizedUrl);
            }

            await _repository.UpdateAsync(existing, cancellationToken);
            return existing;
        }

        var entry = new HistoryEntry
        {
            HistoryId = Guid.NewGuid(),
            ProfileId = profileId,
            Url = sanitizedUrl,
            Title = UrlSanitizer.NormalizeTitle(meaningfulTitle, sanitizedUrl),
            VisitedAtUtc = now,
            VisitCount = 1,
            TransitionKind = transitionKind,
        };

        await _repository.AddAsync(entry, cancellationToken);
        return entry;
    }

    public async Task UpdateTitleAsync(
        Guid profileId,
        string url,
        string title,
        bool retainQueryString = false,
        CancellationToken cancellationToken = default)
    {
        if (IsPlaceholderTitle(title))
        {
            return;
        }

        var sanitizedUrl = UrlSanitizer.SanitizeHttpUrl(url, retainQueryString);
        var existing = await _repository.FindByProfileAndUrlAsync(profileId, sanitizedUrl, cancellationToken);
        if (existing is null)
        {
            return;
        }

        var normalized = UrlSanitizer.NormalizeTitle(title, sanitizedUrl);
        if (!string.Equals(existing.Title, normalized, StringComparison.Ordinal))
        {
            existing.Title = normalized;
            await _repository.UpdateAsync(existing, cancellationToken);
        }
    }

    private static bool IsPlaceholderTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return true;
        }

        var trimmed = title.Trim();
        return string.Equals(trimmed, "Tab Baru", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "New Tab", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "about:blank", StringComparison.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyList<HistoryEntry>> ListAsync(
        Guid profileId,
        string? searchText = null,
        int limit = 200,
        CancellationToken cancellationToken = default)
        => _repository.ListByProfileAsync(profileId, searchText, Math.Clamp(limit, 1, 1000), cancellationToken);

    public Task ClearProfileHistoryAsync(Guid profileId, CancellationToken cancellationToken = default)
        => _repository.ClearByProfileAsync(profileId, cancellationToken);
}
