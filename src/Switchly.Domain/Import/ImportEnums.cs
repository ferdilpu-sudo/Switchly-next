namespace Switchly.Domain.Import;

/// <summary>Supported Chromium-based desktop browsers for local read-only import (REQ-008).</summary>
public enum ImportSourceBrowser
{
    Chrome = 0,
    Edge = 1,
}

/// <summary>Lifecycle status of an import job (ENT-012).</summary>
public enum ImportJobStatus
{
    Preview = 0,
    Running = 1,
    Completed = 2,
    Partial = 3,
    Failed = 4,
    Cancelled = 5,
}

/// <summary>
/// Allowlisted MVP import categories. Cookies, login sessions (REQ-009), and passwords (D1)
/// are intentionally excluded.
/// </summary>
[Flags]
public enum ImportCategories
{
    None = 0,
    Bookmarks = 1 << 0,
    History = 1 << 1,
    OpenTabs = 1 << 2,
}
