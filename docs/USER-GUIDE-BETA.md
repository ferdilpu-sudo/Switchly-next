# Panduan Pengguna & Dukungan — Switchly Beta

**Versi:** `0.9.0-beta.1`  
**OS Resmi:** Windows 11 x64 *(Windows 10 tidak didukung secara resmi pada rilis Beta)*  
**Prasyarat:** [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) (kecuali jika menggunakan build self-contained)

---

## 1. Instalasi & Menjalankan Pertama Kali

Anda dapat memilih salah satu dari dua paket rilis Beta:
1. **Per-User Setup Installer (`Switchly-0.9.0-beta.1-win-x64-setup.exe`)** — memasang Switchly di `%LOCALAPPDATA%\Programs\Switchly` tanpa memerlukan hak Administrator.
2. **Portable ZIP (`Switchly-0.9.0-beta.1-win-x64-portable.zip`)** — ekstrak ke folder yang dapat ditulis user (hindari `C:\Program Files`), lalu jalankan `Switchly.App.exe`.

> **Catatan Windows SmartScreen (Beta):** Paket Beta belum menggunakan sertifikat *Authenticode code-signing*. Jika Windows SmartScreen muncul, pastikan hash SHA-256 file cocok dengan `SHA256SUMS.txt` sebelum memilih *More info → Run anyway*.

> **Penting:** Jalankan Switchly sebagai user standar (**jangan** *Run as Administrator*). Build Release memblokir mode Administrator demi kebijakan sandbox CEF.

---

## 2. Alur Kerja Utama

- **Buat Profil & Atur Grup:** Klik **+ Profil Baru** untuk membuat profil terisolasi. Anda dapat memilih warna avatar dan mencentang 1 atau lebih grup sekaligus.
- **Grup Web / Web-Space Mode:** Klik **+ Grup** dan pilih preset cepat (**ChatGPT**, **Google Flow**, **Dola**, **SeaArt**) atau isi URL custom. Saat Anda memilih grup web lalu mengklik profil mana saja, Switchly membuka ruang kerja khusus situs tersebut (berlabel `Google Flow • Profil A`) tanpa mencampur tab browsing umum.
- **Pindah Task Aktif & Cari Cepat (`Ctrl+K`):** Semua task terbuka otomatis dikelompokkan di bagian **`SEDANG AKTIF (TASK)`** di puncak Sidebar. Tekan **`Ctrl+K`** kapan saja untuk mencari profil dengan cepat, dan klik ikon **Matahari/Bulan** di pojok sidebar untuk beralih tema Dark / Light.
- **Kelola RAM & Auto-Sleep:** Gunakan tombol **`×`** di Sidebar untuk menutup 1 task/sesi, atau **`Tutup Sesi Lain`** / **`Tutup Semua`** di Footer Status Bar. **Auto-Sleep 30m** otomatis menidurkan profil latar belakang yang tidak dipakai 30 menit (dapat di-toggle ON/OFF di pojok kanan footer).
- **Pop-out Multi-Jendela:** Klik tombol **↗ Jendela Baru** pada toolbar profil jika ingin memisahkan profil ke jendela sendiri (misalnya untuk layar ganda / *side-by-side*).
- **Tab & Pemulihan Sesi:** Gunakan tombol `+` untuk tab baru, `Ctrl+Shift+T` untuk membuka kembali tab terakhir yang ditutup, serta tombol **Bookmark / Riwayat / Unduhan** pada toolbar profil.
- **Impor Chrome/Edge:** Klik **Impor Browser** untuk menyalin bookmark, riwayat, dan URL tab terbuka secara *read-only* (tutup Chrome/Edge terlebih dahulu jika database riwayat sedang terkunci).
- **Keamanan, Password Vault & Backup:** Buka dialog **Keamanan & Backup** untuk mengatur Master Password lokal, mengimpor CSV password ke Vault terenkripsi, membuat backup terenkripsi `.swbak`, atau menjadwalkan *restart-to-restore*.

---

## 3. Pemecahan Masalah Umum

| Masalah | Penyebab & Solusi |
|---|---|
| Muncul pesan *tidak dapat dijalankan sebagai Administrator* | Tutup lalu jalankan `Switchly.App.exe` sebagai user Windows standar (non-elevated). |
| Impor riwayat Chrome/Edge gagal karena database terkunci | Tutup semua jendela Chrome/Edge terlebih dahulu, lalu ulangi impor. |
| Penggunaan RAM meningkat saat banyak profil terbuka | Perhatikan indikator RAM di header; tutup profil yang tidak sedang dipakai agar proses renderer dibebaskan. |
| Website meminta login ulang setelah restore di PC lain | Normal: cookie Chromium terikat DPAPI perangkat asal dan sengaja tidak dipindahkan lintas komputer (`ADR-006`). |
| Halaman tertentu crash (*renderer crash*) | Klik tombol **Muat Ulang** pada banner pemulihan di dalam tab terkait tanpa perlu menutup seluruh aplikasi. |

---

## 4. Melaporkan Bug

Sertakan informasi berikut saat melaporkan masalah:
1. Versi Switchly (`0.9.0-beta.1`) dan versi Windows 11 (`winver`).
2. Langkah reproduksi singkat.
3. Potongan log ter-redaksi dari `%LOCALAPPDATA%\SwitchlyNext\Logs\` (periksa terlebih dahulu agar tidak ada informasi pribadi).
