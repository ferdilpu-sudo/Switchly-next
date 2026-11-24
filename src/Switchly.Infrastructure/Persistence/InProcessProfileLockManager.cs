using System.Collections.Concurrent;
using Switchly.Application.Profiles;

namespace Switchly.Infrastructure.Persistence;

/// <summary>Thread-safe in-process lock manager for active/mutating profiles.</summary>
public sealed class InProcessProfileLockManager : IProfileLockManager
{
    private readonly ConcurrentDictionary<Guid, byte> _locks = new();

    public bool TryAcquire(Guid profileId)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("ProfileId must not be empty.", nameof(profileId));
        }

        return _locks.TryAdd(profileId, 0);
    }

    public void Release(Guid profileId) => _locks.TryRemove(profileId, out _);

    public bool IsLocked(Guid profileId) => _locks.ContainsKey(profileId);
}
