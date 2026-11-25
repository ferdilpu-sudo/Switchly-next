# Switchly — UI/UX Design Direction & Feature Preservation (Approved — D14)

**Dokumen:** UI/UX Redesign Direction v1 (Harmonized with D6–D14)
**Tanggal:** 25 November 2026
**Status:** Approved by Product Owner (`D14` in `decisions.md`)
**Produk:** Switchly Next
**Platform:** Windows desktop (WPF / MVVM / .NET 10 / CefSharp CEF Chrome Runtime)

---

## 1. Tujuan & Prinsip Keputusan

Arah desain ini menetapkan **Browser-First · Multi-Profile · Scale-Ready Workspace** (*"One Browser. Multiple Profiles. Seamless Switching."*) tanpa mengurangi fitur existing, mengganti engine CEF, mengubah isolasi `ProfileId`, atau melemahkan kebijakan keamanan.

1. **Preserve before improve:** semua fitur terverifikasi (termasuk D6 Vault/CSV Autofill, D7 Hybrid + Pop-out, D9–D12, D13 Fullscreen) tetap tersedia.
2. **Browser-first:** Browser Shell dan viewport memperoleh ruang layar utama; sidebar navigasi dapat diciutkan penuh saat browsing.
3. **Multi-profile visibility:** pembedaan tegas antara status **Tampil** (`Current`), **Berjalan** (`Running`), **Dijeda** (`Suspended`), **Error**, dan **Tertutup** (`Inactive`).
4. **Scale-ready management:** Profile Library menggunakan **virtualized table/list** (target uji 50, 250, 500, 1.000 profil) tanpa menyalakan proses CEF untuk daftar profil tersimpan.
5. **Security & isolation non-negotiable:** satu `ProfileId` = satu direktori penyimpanan & request context CEF terisolasi.

---

## 2. Resolusi Konflik Resmi (D14)

| Poin | Keputusan Final (D14) |
|---|---|
| **Automatic Website Groups (`D9`)** | Tetap **dihapus dari UI utama**; fokus pada **Manual & Web Workspace Groups**. |
| **Web Workspace Groups (`D10`) & Web-Space Mode (`D11`)** | **Dipertahankan penuh** di Preservation Matrix, Profile Switcher, dan Top Profile Strip (`Group • Profile`). |
| **Shortcut Profile Switcher / Command Palette** | Menggunakan **`Ctrl+Shift+K`** (aman dari benturan `Ctrl+K` milik ChatGPT, Notion, Slack, GitHub). |
| **Bahasa Status & Default Theme (`D8` + §13)** | UI Bahasa Indonesia konsisten (**Tampil / Berjalan / Dijeda / Tertutup**) dengan **Light Theme** sebagai default (toggle Terang/Gelap tetap tersedia). |

---

## 3. Information Architecture

```text
Switchly
├── Browser (Default Experience — Collapsible Navigation)
│   ├── Top Profile Strip (Current + compact Running chips + Web-Space Group badge + Switcher)
│   ├── Tab Strip (per ProfileId + GroupId Web-Space Mode per D11)
│   ├── Navigation Toolbar (Back / Forward / Reload / HTTPS Omnibox / Zoom / Vault / Bookmarks / History / Downloads / Find / Pop-out)
│   └── Browser Viewport (dominant area + HTML5 Fullscreen per D13)
├── Profiles (Virtualized Profile Library — Table/List density)
│   ├── Search, Filters (Semua / Berjalan / Tertutup / Favorit / Arsip), Manual & Web Workspace Groups
│   ├── Sorting, Bulk Actions (with confirmation), Create / Edit / Duplicate / Archive / Delete
├── Activity (Running Profiles, Active Windows/Workspaces, Tab counts, RAM indicators, Close/Sleep controls)
├── Import (Chrome / Edge Import Wizard — no cookies/session import)
├── Security (App Unlock / DPAPI / Master Password / Password Vault + CSV Import per D6)
├── Backup & Restore (Manual AES-256-GCM encrypted backup & staged restart-to-restore)
└── Settings (General/Startup, Appearance Light/Dark, Resource/Auto-Sleep, Privacy)
```

---

## 4. Feature Preservation & Architectural Guardrails

- **Tidak boleh diubah/dihapus:** `CefSharp`/CEF (tanpa WebView2), isolasi `ProfileId` → storage path, `Switchly.BrowserHost` boundary, Hybrid Integrated Workspace + Pop-out Window (`D7`), Web Workspace Groups `DefaultUrl` (`D10`) & Web-Space Mode (`D11`), 3-layer session shutdown + Auto-Sleep (`D12`), HTML5 Fullscreen + proprietary CEF overlay (`D13`), Password Vault + CSV Import + explicit-click autofill (`D6`), Manual Encrypted Backup & Restore, dan 127 unit test baseline.
- **Fitur deferred tetap deferred:** Extension installation parity, scheduled automatic backup, cloud sync, dan remote license/payment backend.
- **Catatan WPF/CEF:** Drag-tab antar-window pada `CefSharp.Wpf` (OSR) ditangani hati-hati secara bertahap (atau via menu *"Pindahkan Tab ke Jendela Baru"*) agar tidak memicu blank frame/re-init CEF.

---

## 5. Rencana Implementasi Bertahap (Phased Plan)

1. **Phase 0 — Feature-to-View Audit:** Petakan semua layar, ViewModel, command, dan entry point fitur existing agar nol fitur hilang.
2. **Phase 1 — Browser Shell (Collapsible Sidebar + Top Profile Strip + `Ctrl+Shift+K` Switcher + Default Light Theme):** Jadikan pengalaman browsing lega tanpa kehilangan akses cepat ke profil/grup aktif (`D10`/`D11`).
3. **Phase 2 — Virtualized Profile Library:** Ganti grid kartu besar menjadi virtualized table/list yang siap 50–1.000 profil dengan search, filter, sorting, dan bulk actions aman.
4. **Phase 3 — Dedicated Activity & Settings Views:** Pisahkan pemantauan sesi berjalan (`Tampil` / `Berjalan` / `Dijeda` / `Error`) dan pengaturan aplikasi secara rapi.
5. **Phase 4 — Regression & Scale Validation:** Verifikasi penuh 127+ unit test, Light/Dark theme, High-DPI, multi-monitor, dan uji performa daftar 1.000 profil.
