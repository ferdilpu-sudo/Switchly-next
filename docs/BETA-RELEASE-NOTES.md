# Catatan Rilis — Switchly 0.9.0-beta.1

**Tanggal:** 2026-11-25  
**Kanal:** Beta (Free-only, offline-ready, tanpa pendaftaran)  
**Target OS Resmi:** Windows 11 x64

## Fitur Utama di Beta

- **Manajemen Profil Terisolasi (`M-003`):** Buat, ubah, duplikasi konfigurasi, arsipkan, favoritkan, cari/filter, dan kelompokkan profil (grup manual & grup website otomatis). Setiap profil memiliki direktori storage CEF Chrome Runtime terpisah (`ADR-011`).
- **Hybrid Integrated Workspace (`D7` & `M-004`):** Pindah profil 1-klik langsung di panel kanan `MainWindow` sambil menjaga profil lain tetap aktif, atau pop-out ke jendela terpisah (`↗ Jendela Baru`). Dilengkapi multi-tab, `Ctrl+Shift+T`, pemulihan sesi tab, serta dialog interaktif Bookmark, Riwayat, dan Unduhan per profil.
- **Impor Chrome/Edge Read-Only (`M-006`):** Deteksi profil lokal, pratinjau, dan impor bookmark, riwayat, serta URL tab terbuka tanpa menyentuh cookie/sesi sumber.
- **Keamanan Lokal & Backup Terenkripsi (`M-007`):** Proteksi lokal berbasis Windows DPAPI, kunci aplikasi opsional dengan Master Password, backup portabel streaming chunked AES-256-GCM, serta *restart-to-restore* dengan rollback aman.
- **Encrypted Password Vault & Autofill (`D6`):** Impor CSV Chrome/Edge ke vault terenkripsi per profil (AES-256-GCM + AAD + DPAPI) dan autofill eksplisit berbasis kecocokan origin.
- **Stabilitas & Pemantauan Resource (`M-008`):** Monitor RAM proses secara berkala, banner peringatan tekanan memori non-modal, pemulihan *renderer crash*, serta dukungan PerMonitorV2 High-DPI.

## Batasan yang Diketahui pada Rilis Beta

- **Windows 10 tidak didukung secara resmi (`OPEN-002`):** Rilis Beta hanya divalidasi dan didukung resmi pada Windows 11 x64.
- **Belum bertanda tangan digital (*unsigned*):** Paket Beta (Inno Setup EXE & Portable ZIP) belum memiliki sertifikat Authenticode; verifikasi integritas melalui `SHA256SUMS.txt`. Code-signing akan diaktifkan sebelum rilis GA.
- **Fitur ditunda ke pasca-Beta:** Dukungan ekstensi (`REQ-007`, M-005), backup terjadwal otomatis (`REQ-017`, D2), serta tier Pro / aktivasi lisensi jarak jauh (Beta sepenuhnya Free tanpa kuota buatan).
- **Kepatuhan situs pihak ketiga:** Switchly tidak melakukan spoofing atau bypass terhadap kebijakan login/keamanan situs web.
