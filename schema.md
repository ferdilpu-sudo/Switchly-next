# Data & Interface Schema — Switchly Next

**Status:** DRAFT 0.1  
**Source of truth:** This document owns local data contracts. Browser engine internal databases remain owned by CEF/Chromium.

## 1. Persistence overview

- Database: SQLite, one local application database.
- Metadata path: `%LOCALAPPDATA%\SwitchlyNext\Data\switchly-next.db`.
- Browser data: `%LOCALAPPDATA%\SwitchlyNext\Profiles\<profile-guid>\` (one directory per profile). `RootCachePath` is `%LOCALAPPDATA%\SwitchlyNext\Profiles`, so each profile directory is a **direct child** of it (required by CEF Chrome Runtime).
- Backup archives: user-selected folder; encrypted before final write.
- No cloud synchronization in MVP.
- Do not query/modify CEF's internal SQLite files directly except through documented read-only import/diagnostic adapters when absolutely needed.

## 2. Entity catalog

| ID | Entity | Key | Purpose | Requirement |
|---|---|---|---|---|
| ENT-001 | BrowserProfile | `ProfileId` GUID | Profile identity and lifecycle metadata | REQ-002, REQ-003 |
| ENT-002 | ProfileGroup | `GroupId` GUID | Manual and system-derived group metadata | REQ-004 |
| ENT-003 | ProfileGroupMembership | `(ProfileId, GroupId)` | Many-to-many membership | REQ-004 |
| ENT-004 | BrowserTabSession | `TabId` GUID | Restorable tab/window metadata | REQ-005, REQ-006 |
| ENT-005 | Bookmark | `BookmarkId` GUID | Application-managed per-profile bookmarks | REQ-006 |
| ENT-006 | HistoryEntry | `HistoryId` GUID | Profile-scoped history projection | REQ-006 |
| ENT-007 | DownloadRecord | `DownloadId` GUID | Download status and file metadata | REQ-006 |
| ENT-008 | ExtensionInstall | `ExtensionInstallId` GUID | Optional extension registry per profile (deferred beyond MVP) | REQ-007 |
| ENT-009 | VaultCredential | `CredentialId` GUID | Imported credentials encrypted at rest | REQ-008, REQ-010 |
| ENT-010 | AppSetting | `Key` string | Application settings | REQ-010, REQ-017 |
| ENT-011 | BackupJob | `BackupJobId` GUID | Backup schedule/run state | REQ-011 |
| ENT-012 | ImportJob | `ImportJobId` GUID | Import history/results | REQ-008 |

## 3. Field contracts

### ENT-001 BrowserProfile
| Field | Type | Required | Constraints |
|---|---|---|---|
| ProfileId | TEXT GUID | Yes | Primary key, immutable |
| Name | TEXT | Yes | 1–80 chars, trimmed |
| ColorHex | TEXT | No | `#RRGGBB`; default theme color |
| IconKey | TEXT | No | Known icon key only |
| StartupUrl | TEXT | No | Valid absolute http/https URL or null |
| StorageRelativePath | TEXT | Yes | App-generated path; never supplied directly by user |
| CreatedAtUtc | TEXT ISO-8601 | Yes | UTC |
| UpdatedAtUtc | TEXT ISO-8601 | Yes | UTC |
| LastOpenedAtUtc | TEXT ISO-8601 | No | UTC |
| IsArchived | INTEGER bool | Yes | Default false |
| RestoreLastSession | INTEGER bool | Yes | Default true |
| PersistSessionCookies | INTEGER bool | Yes | Explicit preference; default selected by product privacy review |

Constraints: unique active profile name is not required; profile IDs and paths must be unique. Do not put password/cookie/session values in this row.

### ENT-002 ProfileGroup
- `GroupId`: GUID primary key.
- `Name`: required, 1–80 characters.
- `Kind`: enum `Manual | WebsiteDomain`.
- `DomainKey`: optional normalized host for WebsiteDomain groups.
- `CreatedAtUtc`, `UpdatedAtUtc`: UTC ISO-8601.
- Unique constraint: `(Kind, DomainKey)` when `Kind=WebsiteDomain`.
- System-derived groups are read-only; users may hide them but should not edit source domain identity.

### ENT-003 ProfileGroupMembership
- `ProfileId`: FK to BrowserProfile.
- `GroupId`: FK to ProfileGroup.
- `MembershipSource`: enum `Manual | Auto`.
- Composite primary key `(ProfileId, GroupId)`.
- Deleting a profile removes memberships; deleting a manual group removes memberships but not profiles.

### ENT-004 BrowserTabSession
- `TabId`: GUID PK.
- `ProfileId`: FK.
- `WindowId`: GUID.
- `Url`: required safe URL; only persist http/https and supported internal URLs.
- `Title`: optional, max 512 chars; treat as untrusted text.
- `Position`: nonnegative integer.
- `IsPinned`: boolean.
- `LastActiveAtUtc`: UTC.
- `RestoreState`: enum `Open | Closed | Discarded`.
- Do not persist form POST bodies, passwords, access tokens, or arbitrary JS state.
- Index `(ProfileId, WindowId, Position)`.

### ENT-005 Bookmark
- `BookmarkId`: GUID PK.
- `ProfileId`: FK.
- `ParentBookmarkId`: nullable self-FK for folders.
- `Title`: required, 1–512 chars.
- `Url`: null for folders; absolute http/https for links.
- `Position`: nonnegative.
- `CreatedAtUtc`, `UpdatedAtUtc`: UTC.
- Index `(ProfileId, ParentBookmarkId, Position)`.

### ENT-006 HistoryEntry
- `HistoryId`: GUID PK.
- `ProfileId`: FK.
- `Url`: required; strip credentials/userinfo; configurable query-string retention.
- `Title`: optional, max 512 chars.
- `VisitedAtUtc`: UTC.
- `VisitCount`: integer >=1.
- `TransitionKind`: enum `Link | Typed | Reload | Restore | Other`.
- Index `(ProfileId, VisitedAtUtc DESC)`.
- Retention/deletion preferences must be respected; do not sync history.

### ENT-007 DownloadRecord
- `DownloadId`: GUID PK.
- `ProfileId`: FK.
- `SuggestedFileName`: max 255 chars; sanitize.
- `TargetPath`: local path; do not log full path by default.
- `MimeType`: optional.
- `BytesReceived`, `TotalBytes`: nullable nonnegative integers.
- `State`: `InProgress | Completed | Cancelled | Failed`.
- `StartedAtUtc`, `CompletedAtUtc`: UTC.
- Browser engine remains authoritative for the active transfer; DB records status/metadata.

### ENT-008 ExtensionInstall
- `ExtensionInstallId`: GUID PK.
- `ProfileId`: FK.
- `ExtensionId`: required validated ID/source key.
- `Name`: required 1–200 chars.
- `Version`: optional.
- `Source`: `ChromeWebStore | LocalFolder | LocalPackage`.
- `InstallPathRelative`: app/profile-relative path only.
- `PermissionSummaryJson`: validated JSON, no secrets.
- `State`: `Pending | Enabled | Disabled | Incompatible | Failed | Removed`.
- Unique `(ProfileId, ExtensionId)`.
- Never assume all Chrome extensions are supported by CEF; extension tables may remain unused until the optional feature is approved.

### ENT-009 VaultCredential
- `CredentialId`: GUID PK.
- `ProfileId`: FK.
- `Origin`: normalized https/http origin; no URL query or fragment.
- `UsernameCiphertext`: encrypted bytes/base64.
- `PasswordCiphertext`: encrypted bytes/base64.
- `CryptoVersion`: integer.
- `Nonce` and `AuthenticationTag`: bytes/base64 where required by chosen cipher.
- `CreatedAtUtc`, `UpdatedAtUtc`: UTC.
- Do not store plaintext credential fields, master password, raw CSV contents, or recoverable keys.
- Encryption key is held/wrapped outside DB; local key wrapping through DPAPI. Backup re-encrypts vault records under portable archive encryption.

### ENT-010 AppSetting
- `Key`: text PK, fixed allowlisted key names.
- `ValueJson`: validated JSON; never store secrets.
- `UpdatedAtUtc`: UTC.
- Settings keys include theme, startup behavior, backup preferences, telemetry opt-in, and resource warnings.

### ENT-011 BackupJob
- `BackupJobId`: GUID PK.
- `Mode`: `Manual | Scheduled`.
- `Status`: `Queued | Running | Completed | Failed | Cancelled`.
- `DestinationPath`: user-selected path; redact in logs.
- `StartedAtUtc`, `CompletedAtUtc`: nullable UTC.
- `IncludedProfilesJson`: list of profile IDs only.
- `ArchiveVersion`: integer.
- `ResultSummaryJson`: counts and non-secret diagnostics.
- No backup passphrase or derived key stored.

### ENT-012 ImportJob
- `ImportJobId`: GUID PK.
- `SourceBrowser`: `Chrome | Edge`.
- `SourceProfileLabel`: user-visible source name; no secrets.
- `SelectedCategoriesJson`: allowlisted category list.
- `TargetProfileId`: FK.
- `Status`: `Preview | Running | Completed | Partial | Failed | Cancelled`.
- `ResultSummaryJson`: counts and validation errors with sensitive values redacted.
- `CreatedAtUtc`, `CompletedAtUtc`: UTC.
- Password values are not stored in ImportJob.

## 4. Relationship overview

```mermaid
erDiagram
    BrowserProfile ||--o{ BrowserTabSession : owns
    BrowserProfile ||--o{ Bookmark : owns
    BrowserProfile ||--o{ HistoryEntry : owns
    BrowserProfile ||--o{ DownloadRecord : owns
    BrowserProfile ||--o{ ExtensionInstall : owns
    BrowserProfile ||--o{ VaultCredential : owns
    BrowserProfile ||--o{ ProfileGroupMembership : grouped
    ProfileGroup ||--o{ ProfileGroupMembership : contains
    BrowserProfile ||--o{ ImportJob : target
```

## 5. Validation and normalization

- GUIDs generated by application; never use names as filesystem paths.
- All filesystem paths are constructed from fixed base path plus validated GUID.
- Normalize domains using IDNA/URI host parsing and lowercase.
- Use HTTPS by default for URLs; HTTP allowed only when user explicitly navigates/imports.
- Reject `javascript:`, `file:`, `data:` and custom schemes from imported bookmarks/restore URLs unless a separate, reviewed browser policy allows them.
- CSV parser must handle quoted fields, UTF-8 and known Chrome/Edge export headers. Reject malformed rows; show counts and redacted errors.
- Never include passwords/CSRF tokens in exception messages.
- Limit row sizes and archive entry counts to protect against memory/disk exhaustion.

## 6. Service/event contracts

No network API is required for the Free MVP. Application services expose typed use cases, e.g.:

```csharp
public sealed record CreateProfileCommand(
    string Name,
    string? StartupUrl,
    string? GroupId);

public sealed record CreateProfileResult(
    Guid ProfileId,
    string StorageRelativePath);

public sealed record ImportPreview(
    int BookmarkCount,
    int HistoryCount,
    int ExtensionCount,
    int OpenTabCount,
    int PasswordCount,
    IReadOnlyList<ImportWarning> Warnings);
```

These are illustrative DTOs, not implemented code. Do not add remote HTTP contracts before the Pro licensing decision.

## 7. Migration policy

- Use versioned EF Core SQLite migrations.
- Apply migrations before opening profile contexts.
- Never delete/rename a column destructively without a staged migration and backup path.
- Migration tests must run against copies of previous schema versions.
- Data files owned by CEF must not be migrated using EF Core.
- Backup archive has its own manifest/schema version and upgrade adapter.
- Restore of an unsupported future archive version must fail safely and leave current data untouched.

## 8. Deletion, privacy and retention

- Deleting a profile: close browser/context, confirm, optionally create backup, remove metadata and directory.
- Clearing history: delete only selected profile records and call matching browser-data cleanup where required.
- Deleting app metadata does not claim to securely overwrite SSD sectors.
- Credential vault deletion removes encrypted records and rotates/invalidates the relevant wrapped key where appropriate.
- Backup retention default: `Terbuka`; proposed initial default 7 daily snapshots, configurable.
- Windows account or master password changes must be handled by a documented key recovery policy.

## 9. Terbuka

- Exact browser history DB schemas for the supported Chrome/Edge versions; validated in import spike.
- Which limited extension APIs, if any, are worth supporting after the MVP.
- Password autofill integration versus secure local vault management; do not promise autofill until proof-of-concept.
- Whether profile metadata DB is encrypted as a whole; vault fields must be encrypted regardless.
- Backup default schedule and retention duration.
