namespace Switchly.Application.Security;

public sealed record LocalSecurityStatus(
    bool IsProtectionHealthy,
    bool IsMasterPasswordEnabled,
    bool IsWindowsHelloAvailable,
    string? ErrorMessage = null);

/// <summary>Manages DPAPI-protected local unlock state and optional master password (TASK-029, REQ-010).</summary>
public interface ILocalSecurityService
{
    LocalSecurityStatus GetStatus();

    void EnableMasterPassword(string masterPassword);

    bool VerifyMasterPassword(string candidatePassword);

    bool DisableMasterPassword(string currentMasterPassword);
}
