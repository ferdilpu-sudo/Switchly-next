# Catatan Rilis — Switchly 0.9.0-beta.1

**Tanggal:** 2026-11-25  
**Kanal:** Beta (Free-only, offline-ready, tanpa pendaftaran)  
**Target OS Resmi:** Windows 11 x64

## Fitur Utama di Beta

- **Manajemen Profil Terisolasi (`M-003` & `D9`):** Buat, ubah, duplikasi konfigurasi, arsipkan, favoritkan, cari cepat (`Ctrl+K`), dan masukkan 1 profil ke banyak grup sekaligus (multi-select). Setiap profil memiliki direktori storage CEF Chrome Runtime terpisah (`ADR-011`).
- **Modern Dark & Light UI + Web-Space Mode (`D7`, `D10`, `D11`):** Tema Dark & Light dengan toggle 1-klik (sinkron title bar Windows 11) dan 100% ikon vektor. Grup Web (*preset* ChatGPT, Google Flow, Dola, SeaArt, atau custom) membuka ruang kerja & tab terisolasi per `(Grup, Profil)` tanpa mencampur tab situs lain, dilengkapi daftar **`SEDANG AKTIF (TASK)`** terkelompok di Sidebar untuk pindah task dalam 1 klik, pop-out jendela terpisah, multi-tab, `Ctrl+Shift+T`, pemulihan sesi, serta dialog Bookmark/Riwayat/Unduhan.
- **Kontrol Sesi & Pemantauan Resource (`M-008` & `D12`):** Monitor RAM berkala, tombol `×` 1-klik di Sidebar, **`Tutup Sesi Lain`** / **`Tutup Semua`** di Footer & Banner Peringatan Memori, **Auto-Sleep 30 menit** untuk profil latar belakang yang *idle* (dapat di-toggle ON/OFF), pemulihan *renderer crash*, serta dukungan PerMonitorV2 High-DPI.
- **Impor Chrome/Edge Read-Only (`M-006`):** Deteksi profil lokal, pratinjau, dan impor bookmark, riwayat, serta URL tab terbuka tanpa menyentuh cookie/sesi sumber.
- **Keamanan Lokal & Backup Terenkripsi (`M-007`):** Proteksi lokal berbasis Windows DPAPI, kunci aplikasi opsional dengan Master Password, backup portabel streaming chunked AES-256-GCM, serta *restart-to-restore* dengan rollback aman.
- **Encrypted Password Vault & Autofill (`D6`):** Impor CSV Chrome/Edge ke vault terenkripsi per profil (AES-256-GCM + AAD + DPAPI) dan autofill eksplisit berbasis kecocokan origin.

## Batasan yang Diketahui pada Rilis Beta

- **Windows 10 tidak didukung secara resmi (`OPEN-002`):** Rilis Beta hanya divalidasi dan didukung resmi pada Windows 11 x64.
- **Belum bertanda tangan digital (*unsigned*):** Paket Beta (Inno Setup EXE & Portable ZIP) belum memiliki sertifikat Authenticode; verifikasi integritas melalui `SHA256SUMS.txt`. Code-signing akan diaktifkan sebelum rilis GA.
- **Fitur ditunda ke pasca-Beta:** Dukungan ekstensi (`REQ-007`, M-005), backup terjadwal otomatis (`REQ-017`, D2), serta tier Pro / aktivasi lisensi jarak jauh (Beta sepenuhnya Free tanpa kuota buatan).
- **Kepatuhan situs pihak ketiga:** Switchly tidak melakukan spoofing atau bypass terhadap kebijakan login/keamanan situs web.
