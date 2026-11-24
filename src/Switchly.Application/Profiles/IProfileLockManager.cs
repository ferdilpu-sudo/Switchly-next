namespace Switchly.Application.Profiles;

/// <summary>In-process per-profile state lock preventing concurrent lifecycle mutations or deletion.</summary>
public interface IProfileLockManager
{
    bool TryAcquire(Guid profileId);

    void Release(Guid profileId);

    bool IsLocked(Guid profileId);
}
