# Microsoft Store Listing & Submission Guide — Switchly (`9PH5FF24TKZR`)

**Website:** https://www.switchly.site/  
**Format Paket:** MSIX (`win-x64`, self-contained .NET 10 + Chromium/CEF, `runFullTrust`)  
**Code-Signing:** Otomatis & **Gratis** ditandatangani oleh Microsoft Store saat upload di Partner Center.

---

## 1. Copywriting Store Listing (Bahasa Indonesia)

### Deskripsi Singkat (Short Description)
> Kelola banyak akun website dalam satu workspace desktop Windows 11 dengan isolasi profil penuh, tanpa logout-login berulang.

### Deskripsi Lengkap (Description)
Switchly adalah ruang kerja browser desktop multi-akun untuk Windows 11 yang dirancang agar Anda dapat membuka dan berpindah antar banyak akun website secara bersamaan tanpa saling mencampur sesi login.

**Fitur Utama:**
- **Isolasi Profil Penuh:** Setiap profil memiliki cookie, sesi login, cache, bookmark, riwayat, dan unduhan yang benar-benar terpisah. Cocok untuk mengelola banyak akun AI, media sosial, maupun akun kerja.
- **Antarmuka Browser-First (Tema Terang & Gelap):** Sidebar yang dapat dilipat (`Ctrl+B`), Top Profile Strip untuk pindah sesi aktif dalam 1 klik, Pindah Profil Cepat (`Ctrl+Shift+K`), jendela pop-out terpisah, dan dukungan layar ganda.
- **Grup & Ruang Kerja Web:** Kelompokkan profil ke banyak grup sekaligus, lengkap dengan preset ruang kerja web (ChatGPT, Google Flow, Dola, SeaArt, atau URL kustom).
- **Pemantauan Aktivitas & Auto-Sleep RAM:** Halaman Aktivitas untuk memantau RAM per sesi, peringatan memori otomatis di ≥ 80% RAM, serta fitur Auto-Sleep 30 menit untuk sesi latar belakang yang tidak dipakai.
- **Local-First & Privasi Terjaga:** Tanpa wajib akun untuk mode Free dan tanpa telemetri. Dilengkapi Password Vault terenkripsi lokal (AES-256 + enkripsi bawaan Windows), impor read-only dari Chrome/Edge, serta backup/restore manual terenkripsi.

**Catatan Privasi & Kepatuhan:**
Switchly menghormati kebijakan autentikasi dan keamanan seluruh situs web pihak ketiga. Data browsing tersimpan secara lokal di perangkat Anda.

### Yang Baru di Versi 2.0 (What's New in This Version)
- **Mesin Browser Baru (Chromium 156 Terisolasi):** Arsitektur baru dengan isolasi penyimpanan penuh per profil, performa lebih stabil, multi-tab, fullscreen video HTML5, dan pemulihan crash per tab.
- **Desain Baru Browser-First:** Mode Terang & Gelap, Sidebar lipat (`Ctrl+B`), Pindah Profil Cepat (`Ctrl+Shift+K`), halaman Aktivitas, dan Filter Grup ketat.
- **Efisiensi Memori:** Auto-Sleep 30 menit otomatis untuk sesi latar belakang + bilah peringatan memori non-intrusif.
- **Keamanan & Backup Baru:** Password Vault terenkripsi lokal, impor bookmark/riwayat/tab dari Chrome & Edge, serta cadangan terenkripsi `.swbak`.
- **Catatan Pembaruan dari v1:** Karena perpindahan ke mesin browser Chromium baru, sesi login dari versi lama tidak terbawa otomatis — silakan login ulang satu kali pada profil baru Anda.

---

## 2. Copywriting Store Listing (English)

### Short Description
> Multi-account Windows 11 desktop browser workspace with true per-profile session isolation — switch accounts without logging out.

### What's New in 2.0
- Brand-new isolated Chromium 156 engine with strict per-profile cookies, storage, bookmarks, history, and downloads.
- Browser-First UI redesign: Light & Dark themes, collapsible sidebar (`Ctrl+B`), Quick Profile Switcher (`Ctrl+Shift+K`), Activity monitor, and Web Workspace Groups.
- Built-in memory management with 30-minute Auto-Sleep and non-intrusive RAM advisory strip.
- Local encrypted Password Vault, read-only Chrome/Edge import, and manual AES-256 encrypted backups.
- Note for v1 users: Due to the engine upgrade to isolated Chromium profiles, existing v1 website logins will require signing in once in your new profiles.

---

## 3. Checklist Submit di Microsoft Partner Center (`9PH5FF24TKZR`)

1. **Ambil Identitas Paket Lama:**
   Buka Partner Center → **Switchly (`9PH5FF24TKZR`)** → **Product management → Product identity**, lalu catat:
   - `Package/Identity/Name`
   - `Package/Identity/Publisher`
   - `Package/Properties/PublisherDisplayName`
2. **Siapkan Logo MSIX (opsional tapi disarankan):**
   Letakkan PNG logo Switchly di `packaging/msix/Assets/`: `StoreLogo.png` (50×50), `Square44x44Logo.png` (44×44), `Square150x150Logo.png` (150×150), `Wide310x150Logo.png` (310×150). *(Jika belum ada, script membuat ikon monogram `S` otomatis.)*
3. **Bangun Paket MSIX (setelah overlay CEF proprietary selesai):**
   ```powershell
   .\packaging\package-msix.ps1 `
     -IdentityName "<Package/Identity/Name dari Partner Center>" `
     -Publisher "<Package/Identity/Publisher dari Partner Center>" `
     -PublisherDisplayName "Switchly" `
     -Version "2.0.0.0"
   ```
4. **Notes for Certification (wajib diisi karena `runFullTrust`):**
   > *"Switchly is a .NET 10 WPF desktop browser workspace powered by Chromium Embedded Framework (CEF), which requires `runFullTrust` to spawn isolated Chromium renderer/GPU subprocesses and manage local per-profile browser storage under the user's AppData directory. Free features work completely offline without requiring an account."*
5. **Privacy Policy URL:** `https://www.switchly.site/privacy` (publikasikan isi `docs/PRIVACY.md` di halaman ini).
6. **Upload `.msix`** ke Submission baru di Partner Center → Microsoft Store akan otomatis menandatangani dan mempublikasikan update ke pengguna `9PH5FF24TKZR`.
