using Switchly.Domain.Downloads;

namespace Switchly.Application.Browser;

public sealed class DownloadService : IDownloadService
{
    private readonly IDownloadRepository _repository;
    private readonly TimeProvider _timeProvider;

    public DownloadService(IDownloadRepository repository, TimeProvider? timeProvider = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<DownloadRecord> RecordOrUpdateAsync(DownloadUpdateEvent update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        var safeFileName = DownloadFileNameSanitizer.Sanitize(update.SuggestedFileName);
        var now = _timeProvider.GetUtcNow();
        var isTerminal = update.State is DownloadState.Completed or DownloadState.Cancelled or DownloadState.Failed;

        var existing = await _repository.FindByProfileAndTargetPathAsync(update.ProfileId, update.TargetPath, cancellationToken);
        if (existing is not null)
        {
            existing.SuggestedFileName = safeFileName;
            existing.MimeType = update.MimeType;
            existing.BytesReceived = Math.Max(0, update.BytesReceived);
            existing.TotalBytes = update.TotalBytes >= 0 ? update.TotalBytes : null;
            existing.State = update.State;
            if (isTerminal && existing.CompletedAtUtc is null)
            {
                existing.CompletedAtUtc = now;
            }

            await _repository.UpdateAsync(existing, cancellationToken);
            return existing;
        }

        var record = new DownloadRecord
        {
            DownloadId = Guid.NewGuid(),
            ProfileId = update.ProfileId,
            SuggestedFileName = safeFileName,
            TargetPath = update.TargetPath,
            MimeType = update.MimeType,
            BytesReceived = Math.Max(0, update.BytesReceived),
            TotalBytes = update.TotalBytes >= 0 ? update.TotalBytes : null,
            State = update.State,
            StartedAtUtc = now,
            CompletedAtUtc = isTerminal ? now : null,
        };

        await _repository.AddAsync(record, cancellationToken);
        return record;
    }

    public Task<IReadOnlyList<DownloadRecord>> ListByProfileAsync(Guid profileId, int limit = 100, CancellationToken cancellationToken = default)
        => _repository.ListByProfileAsync(profileId, Math.Clamp(limit, 1, 500), cancellationToken);

    public Task MarkInterruptedOnStartupAsync(CancellationToken cancellationToken = default)
        => _repository.MarkAllInProgressAsFailedAsync(_timeProvider.GetUtcNow(), cancellationToken);
}
