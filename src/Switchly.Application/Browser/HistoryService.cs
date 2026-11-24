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
        var now = _timeProvider.GetUtcNow();
        var existing = await _repository.FindByProfileAndUrlAsync(profileId, sanitizedUrl, cancellationToken);

        if (existing is not null)
        {
            existing.VisitCount += 1;
            existing.VisitedAtUtc = now;
            existing.TransitionKind = transitionKind;
            if (!string.IsNullOrWhiteSpace(title))
            {
                existing.Title = UrlSanitizer.NormalizeTitle(title, sanitizedUrl);
            }

            await _repository.UpdateAsync(existing, cancellationToken);
            return existing;
        }

        var entry = new HistoryEntry
        {
            HistoryId = Guid.NewGuid(),
            ProfileId = profileId,
            Url = sanitizedUrl,
            Title = string.IsNullOrWhiteSpace(title) ? null : UrlSanitizer.NormalizeTitle(title, sanitizedUrl),
            VisitedAtUtc = now,
            VisitCount = 1,
            TransitionKind = transitionKind,
        };

        await _repository.AddAsync(entry, cancellationToken);
        return entry;
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
