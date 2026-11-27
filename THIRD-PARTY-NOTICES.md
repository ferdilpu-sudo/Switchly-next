# Third-Party Notices — Switchly

Switchly incorporates third-party open-source software components. Each component is governed by its own license terms.

## 1. Runtime & Application Dependencies (Pinned in `Directory.Packages.props`)

| Component | Version | License | Upstream |
|---|---|---|---|
| **CefSharp.Wpf.NETCore** (and transitive `CefSharp.Common.NETCore`, `chromiumembeddedframework.runtime.win-x64`) | `156.0.300` | BSD-3-Clause (CefSharp) / BSD-3-Clause & Chromium third-party licenses (CEF `156.0.30` / Chromium `156.0.8078.96`) | https://github.com/cefsharp/CefSharp |
| **Chromium Embedded Framework (CEF)** & **Chromium** | `156.0.30` / `156.0.8078.96` | BSD-3-Clause + Chromium component licenses (including FFmpeg LGPL/BSD notice where applicable) | https://bitbucket.org/chromiumembedded/cef |
| **BouncyCastle.Cryptography** | `2.6.2` | MIT License | https://github.com/bcgit/bc-csharp |
| **Microsoft .NET 10 Runtime / WPF / `Microsoft.Extensions.*`** | `10.0.11` | MIT License | https://github.com/dotnet/runtime |
| **Microsoft.EntityFrameworkCore.Sqlite** & **Design** | `10.0.11` | MIT License | https://github.com/dotnet/efcore |
| **SQLite / `SQLitePCLRaw` / `e_sqlite3`** | Transitive via EF Core `10.0.11` | Public Domain (SQLite) / Apache-2.0 (`SQLitePCLRaw`) | https://www.sqlite.org/copyright.html |

## 2. Server-Only Dependencies (`server/Switchly.LicenseApi` — Not Shipped in Desktop Packages)

| Component | Version | License | Upstream |
|---|---|---|---|
| **Npgsql.EntityFrameworkCore.PostgreSQL** & **Npgsql** | `10.0.0` | PostgreSQL License | https://github.com/npgsql/efcore.pg |

## 3. Test-Only Dependencies (Not Redistributed in Release Packages)

| Component | Version | License |
|---|---|---|
| **xUnit (`xunit`, `xunit.runner.visualstudio`)** | `2.9.3` / `3.1.4` | Apache-2.0 |
| **Microsoft.NET.Test.Sdk** | `17.14.1` | MIT License |
| **coverlet.collector** | `6.0.4` | MIT License |

## 4. Redistribution Checklist (Before Public Distribution)

- Ship this `THIRD-PARTY-NOTICES.md` alongside the Microsoft Store MSIX, Portable ZIP, and Inno Setup installer.
- Preserve CEF/Chromium license files (`LICENSE.txt` / `credits.html` when bundled by the CEF runtime package) in the distribution directory.
- Before wide commercial distribution with proprietary media codecs (`H.264`/`AAC`), complete separate patent/licensing review (MPEG-LA / Via LA).
