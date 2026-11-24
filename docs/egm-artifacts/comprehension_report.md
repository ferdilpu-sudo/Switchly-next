# Comprehension Goldfish — Full Specification Corpus Audit

**Reviewer:** egm-comprehension (Goldfish — fresh context)  
**Scope:** Full specification corpus (9 documents)  
**Date:** 2026-11-24  
**Status:** Report delivered inline; subagent terminated with model error after completing review.

---

### What this proposes
Switchly Next is a Windows desktop application that lets users manage multiple website accounts without repeated logout/login. Each "profile" gets its own isolated Chromium browser storage (cookies, localStorage, cache, etc.) via CefSharp/CEF request contexts with unique persistent cache paths. The app uses WPF/MVVM on .NET 10, SQLite for app metadata, DPAPI for local secret protection, and AES-256-GCM encrypted backup archives with passphrase-derived keys. Users can import bookmarks, history, open tabs, and password CSVs from Chrome/Edge. Extensions are explicitly deferred. Freemium model is planned but undefined.

### How the existing system works, per this document
There is no existing system or code. The corpus is a pre-implementation specification suite: PRD, architecture, schema, design, decisions, rules, testing, plan, and README. All milestones are "Not started." The specification defines nine canonical documents with an authority hierarchy. The first gate (M-001) is a CEF proof-of-concept that must prove profile isolation, priority website compatibility, and resource baselines before any product code is built.

---

## 1. UNDEFINED TERMS AND ACRONYMS

| # | Term | Document | Section | Severity |
|---|------|----------|---------|----------|
| 1 | **"Google Flow"** | prd.md §3, architecture.md §1, testing.md §4 | Priority provider list | 🔴 Blocker — This is listed as a primary compatibility target, but "Google Flow" is not a commonly recognized Google product name. Is this Google Workspace? Google One? A specific Google product? An implementer would not know what URL to test. |
| 2 | **"Dola"** | prd.md §3, architecture.md §1, testing.md §4 | Priority provider list | 🟡 Major — "Dola" is not an immediately recognizable service. No URL or description is provided. An implementer cannot test compatibility without knowing the target site. |
| 3 | **"SeaArt"** | prd.md §3, architecture.md §1, testing.md §4 | Priority provider list | 🟢 Nit — SeaArt AI is a known AI art generation platform, but a URL or brief description would eliminate ambiguity. |
| 4 | **"Terbuka"** | prd.md §7, schema.md §8/§9, design.md §6/§9 | Multiple sections | 🟢 Nit — Indonesian for "Open" (status). Used inconsistently: sometimes as a status label inline (e.g., `Status: Terbuka`), sometimes as a section heading. Consistent with the document's Indonesian language, but mixed with English status labels like "Proposed" and "Confirmed" in decisions.md. |
| 5 | **"Asumsi"** | prd.md §7 | NFR security | 🟢 Nit — Indonesian for "Assumption." Same mixed-language issue as "Terbuka." |
| 6 | **`CefRequestContext`** | architecture.md §5.1, design.md §4.1 | Profile isolation model | 🟢 Nit — Defined by context and linked to CEF docs in architecture.md §5.1 line 133. Acceptable. |
| 7 | **`CachePath`** | architecture.md §5.1 | Profile isolation | 🟢 Nit — Defined by context in architecture.md. |
| 8 | **KDF** | decisions.md §7, README.md §2 | ADR-006 | 🟡 Major — "KDF" (Key Derivation Function) is used without expansion. While common in security contexts, the specific KDF algorithm (PBKDF2? Argon2? scrypt?) is never named, which is a gap for an implementer. |
| 9 | **IDNA** | schema.md §5 | Validation and normalization | 🟢 Nit — "IDNA" (Internationalized Domain Names in Applications) used without expansion. Standard term but could benefit from a brief note. |
| 10 | **"Chrome Runtime"** vs **"standard CEF"** | architecture.md §2, decisions.md §4, AGENTS.md | Multiple | 🟡 Major — The docs distinguish "standard CEF runtime" from "Chrome Runtime" (a CEF build mode), but never define what this distinction means. An implementer unfamiliar with CEF would not understand why this matters or what build flags are involved. |

## 2. AMBIGUOUS REQUIREMENTS

| # | Requirement/Section | Document | Ambiguity | Severity |
|---|---------------------|----------|-----------|----------|
| 1 | **REQ-002**: "Penggandaan tidak boleh menyalin sesi rahasia secara diam-diam" | prd.md §4.1 | What constitutes "sesi rahasia" (secret session)? Does this mean cookies, localStorage, vault credentials, all of the above? The boundary between "configuration" that IS copied and "secret session data" that is NOT copied is undefined. rules.md §3 clarifies "copies profile settings only" but prd.md alone is ambiguous. | 🟡 Major |
| 2 | **REQ-004**: "grup otomatis berdasarkan website/domain yang dikenali" | prd.md §4.1 | What does "dikenali" (recognized) mean? Is auto-grouping based on the domains the user has visited? The startup URL? Some predefined list? schema.md defines `WebsiteDomain` kind with `DomainKey`, but the trigger/source for auto-group creation is never specified. | 🟡 Major |
| 3 | **REQ-005**: "sistem mengelola resource tanpa kuota aktif tetap" | prd.md §4.1 | No quantitative guidance on what "manages resources" means. When does a warning appear? What thresholds? architecture.md §5.3 says "warn and offer close/suspend" but the trigger conditions are undefined. The docs say "don't set universal limits before measuring" (fair pre-M-001), but after M-001 this needs to become concrete. | 🟢 Nit (acceptable pre-implementation) |
| 4 | **AC-004**: "memulihkan data sesi persistennya selama website masih mempertahankan sesi" | prd.md §6 | The qualifier "selama website masih mempertahankan sesi" (as long as the website still maintains the session) makes this AC untestable by the app team alone. The pass/fail depends on third-party behavior. | 🟡 Major |
| 5 | **ENT-001 `PersistSessionCookies`**: "default selected by product privacy review" | schema.md §3 | No default value is specified. An implementer doesn't know whether to default to true or false. | 🟡 Major |
| 6 | **design.md §6 line 133**: "Bahasa Indonesia; localization framework dipakai agar bahasa lain dapat ditambahkan. Terbuka: kebutuhan English UI." | design.md §6 | The MVP UI language is Indonesian, but all priority test targets (Google Flow, ChatGPT, Dola, SeaArt) are English-language services. Is the application chrome in Indonesian while browsing English sites? This seems intentional but could confuse an international implementer or tester. | 🟢 Nit |
| 7 | **Duplicate paragraph** in prd.md lines 33-36 and architecture.md lines 9-12 | prd.md §3, architecture.md §1 | The priority provider paragraph is duplicated verbatim within each file (prd.md has it at lines 33 AND 36; architecture.md has it at lines 9 AND 12). This looks like a copy-paste error. | 🟢 Nit |
| 8 | **"synthetic local test site"** in TEST-003 | testing.md §3 | TEST-003 says "Login to synthetic local test site in Profile A." No guidance on what this test site should be, how to set it up, or what authentication mechanism to use. An implementer would need to build this. | 🟡 Major |

## 3. UNTESTABLE ACCEPTANCE CRITERIA

| # | AC ID | Issue | Severity |
|---|-------|-------|----------|
| 1 | **AC-004** | "memulihkan data sesi persistennya **selama website masih mempertahankan sesi**" — The pass/fail condition depends on external website behavior, not the application. The application can only guarantee it restores its own persistent storage; whether a website honors the restored cookies is outside the app's control. Suggest rewriting: "reopening a profile restores its persistent CEF storage directory and configured tabs; cookie/session validity depends on the provider." | 🟡 Major |
| 2 | **AC-013** | "Semua fitur yang belum masuk Free/Pro diperlakukan sebagai Terbuka, bukan dibatasi secara arbitrer" — How does a tester verify "not arbitrarily restricted"? There is no enumerated list of features to check. The AC needs a concrete checklist or inspection method. | 🟡 Major |
| 3 | **AC-018** | "peringatan ketika tekanan resource melewati ambang yang telah diuji" — The threshold values are undefined. Without knowing what "tested threshold" is, a tester cannot construct a pass/fail scenario. This is acceptable only until M-001 baselines are established; after that, the threshold must be specified. | 🟡 Major |
| 4 | **AC-008** | "ekstensi yang tidak didukung harus ditolak atau ditandai dengan alasan yang akurat" — "Accurate reason" is subjective. What constitutes an accurate reason? The CEF error code? A human-readable message? Both? | 🟢 Nit |

## 4. FLOW-TO-SCREEN GAPS

| # | Flow (prd.md) | Screen (design.md) | Gap | Severity |
|---|---------------|---------------------|-----|----------|
| 1 | **FLOW-001 step 2** | **UI-002 Profile Create/Edit** | ✅ Covered. | — |
| 2 | **FLOW-002** | **UI-001 Main Workspace** | ✅ Covered. | — |
| 3 | **FLOW-003 step 7** | **UI-009 Import Wizard** | ✅ Covered. | — |
| 4 | **FLOW-004** | **UI-011 Backup & Restore** | ✅ Covered. | — |
| 5 | **FLOW-005** | **UI-008 Optional Extension Settings** | ✅ Covered as deferred/optional. | — |
| 6 | **Missing: Profile archiving flow** | **UI-004 Profile Context Menu** | 🟡 Major — REQ-002 says users can "archive" profiles, and UI-004's context menu lists "archive." But no FLOW describes the archive interaction (what happens when a profile is archived? Can it be unarchived? What's visible vs hidden?). prd.md has no FLOW for archive/unarchive. |
| 7 | **Missing: Password vault viewing/management** | No screen | 🟡 Major — REQ-010 and ENT-009 define a credential vault. TEST-011 tests unlock. But there is no UI screen for viewing, searching, or managing stored credentials. Can the user see their imported passwords? Copy them? Delete individual entries? No flow or screen addresses this. |
| 8 | **Missing: Profile deletion confirmation flow** | **UI-004** | 🟢 Nit — Described in design.md §4.1 line 74 but has no dedicated screen ID. Acceptable as a modal. |
| 9 | **Missing: Onboarding/first-run experience** | No screen | 🟢 Nit — No flow or screen describes what happens on first launch. |

## 5. TEST TRACEABILITY GAPS

| # | AC / REQ | Test Coverage | Gap | Severity |
|---|----------|---------------|-----|----------|
| 1-8 | AC-002 through AC-013 | Covered | — | — |
| 9 | **AC-004** | **TEST-004** | 🟡 Major — Test should explicitly verify CEF storage persistence, not website session validity. |
| 10 | **REQ-002 "archive"** | No specific test | 🟡 Major — No TEST covers archive-specific behavior. TEST-002 mentions "archive" in steps but doesn't verify archive-specific outcomes. |
| 11 | **REQ-010 Windows Hello** | **TEST-011** | 🟡 Major — No distinction between Hello-present vs Hello-absent scenarios. |
| 12 | **NFR-PERF-001 through 004** | **TEST-016** | 🟢 Nit — Doesn't explicitly verify closed profiles don't create renderers. |

## 6. COMPREHENSION BLOCKERS

| # | Location | Issue | Severity |
|---|----------|-------|----------|
| 1 | **"Google Flow" everywhere** | 🔴 Blocker — Undefined product; blocks M-001 TASK-003. |
| 2 | **Profile archive lifecycle** | 🟡 Major — Undefined behavior. |
| 3 | **Auto-group creation trigger** | 🟡 Major — WHEN/HOW auto-groups are created is unspecified. |
| 4 | **Vault credential UX** | 🟡 Major — No screen/flow for viewing/managing stored credentials. |
| 5 | **Master password setup flow** | 🟡 Major — No flow for initial setup, change, or forgotten password. |
| 6 | **`persist_session_cookies` default** | 🟡 Major — No default value specified. |
| 7 | **Mixed language** | 🟢 Nit — Consistent but mixed Indonesian/English. |
| 8 | **README §4 clone directory mismatch** | 🟢 Nit — `cd Switchly-Browser` doesn't match solution name. |
| 9 | **Duplicate section numbering in README** | 🟢 Nit — Two sections numbered "9." |

---

### Summary Counts

| Severity | Count |
|----------|-------|
| 🔴 Blocker | 1 |
| 🟡 Major | 16 |
| 🟢 Nit | 12 |
