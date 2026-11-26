# Panduan Pengguna & Dukungan — Switchly

**Versi:** `0.9.0-beta.1`  
**OS Resmi:** Windows 11 x64 *(Windows 10 tidak didukung secara resmi)*  
**Prasyarat:** [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) *(tidak diperlukan jika menggunakan paket self-contained)*

---

## 1. Instalasi & Menjalankan Pertama Kali

Anda dapat memilih salah satu dari dua paket instalasi:
1. **Installer Pengguna (`Switchly-0.9.0-beta.1-win-x64-setup.exe`)** — memasang Switchly di `%LOCALAPPDATA%\Programs\Switchly` tanpa memerlukan hak Administrator.
2. **Portable ZIP (`Switchly-0.9.0-beta.1-win-x64-portable.zip`)** — ekstrak ke folder yang dapat ditulis user (hindari `C:\Program Files`), lalu jalankan `Switchly.App.exe`.

> **Catatan Windows SmartScreen (Beta):** Paket Beta belum menggunakan sertifikat tanda tangan digital (*code-signing*). Jika Windows SmartScreen muncul, pastikan hash SHA-256 file cocok dengan `SHA256SUMS.txt` sebelum memilih *More info → Run anyway*.

> **Penting:** Jalankan Switchly secara normal sebagai pengguna Windows standar (**jangan** klik kanan *Run as Administrator*). Mode Administrator diblokir demi keamanan sandbox browser.

---

## 2. Alur Kerja Utama

- **Buat Profil & Atur Grup:** Klik **+ Profil Baru** untuk membuat profil browser terisolasi. Anda dapat memilih warna avatar, URL awal, dan menandai 1 atau lebih grup sekaligus.
- **Filter Grup & Ruang Kerja Web:** Klik **+ Grup** dan pilih preset cepat (**ChatGPT**, **Google Flow**, **Dola**, **SeaArt**) atau isi URL situs pilihan Anda. Memilih sebuah grup di Sidebar akan memfilter daftar profil khusus untuk grup tersebut; jika belum ada profil di grup itu, klik tombol **+ Profil Baru** untuk langsung menambahkannya.
- **Pindah Profil Cepat (`Ctrl+Shift+K`) & Sidebar Ringkas (`Ctrl+B`):** Tekan **`Ctrl+Shift+K`** kapan saja untuk mencari dan berpindah profil tanpa bentrok dengan pintasan situs web (`Ctrl+K`). Gunakan **`Ctrl+B`** untuk melipat atau membuka Sidebar agar area browser lebih lega, serta gunakan **Top Profile Strip** di bagian atas untuk berpindah antar sesi yang sedang aktif dalam 1 klik.
- **Pantau Aktivitas, Memori & Auto-Sleep:** Buka menu **Aktivitas** di Sidebar untuk melihat seluruh sesi berjalan dan penggunaan RAM, atau aktifkan **Auto-Sleep (30 menit)** di **Pengaturan** untuk otomatis menutup sesi latar belakang yang tidak dipakai. Jika RAM sistem mencapai ≥ 80%, Switchly menampilkan bilah peringatan di bagian atas (tanpa menutupi halaman web) agar Anda dapat menutup sesi latar belakang dengan cepat.
- **Jendela Terpisah (Pop-out):** Klik tombol **↗ Jendela Baru** pada baris profil atau toolbar browser untuk membuka profil di jendela tersendiri (cocok untuk layar ganda / *side-by-side*).
- **Tab, Bookmark, Riwayat & Unduhan:** Gunakan `+` untuk tab baru, `Ctrl+Shift+T` untuk membuka kembali tab terakhir yang ditutup, serta tombol **Bookmark / Riwayat / Unduhan** pada toolbar profil.
- **Impor Chrome/Edge:** Buka **Pengaturan → Impor dari Chrome / Edge** untuk menyalin bookmark, riwayat, dan URL tab terbuka secara *read-only* tanpa menyentuh cookie browser asal.
- **Keamanan, Password Vault & Cadangan:** Buka **Pengaturan → Keamanan, Vault & Cadangan** untuk mengaktifkan kata sandi utama lokal, mengimpor file CSV password ke Vault terenkripsi, membuat cadangan terenkripsi `.swbak`, atau memulihkan cadangan di PC lain.
- **Akun & Lisensi (Opsional):** Pada masa Beta semua fitur gratis tanpa akun. Di **Pengaturan → Akun & Lisensi**, tersedia pengelolaan akun OTP opsional untuk Trial Pro 7 hari dan paket Pro saat rilis penuh.

---

## 3. Pemecahan Masalah Umum

| Masalah | Penyebab & Solusi |
|---|---|
| Muncul pesan *tidak dapat dijalankan sebagai Administrator* | Tutup lalu jalankan `Switchly.App.exe` secara normal (tanpa *Run as Administrator*). |
| Impor riwayat Chrome/Edge gagal karena file sedang dipakai | Tutup semua jendela Chrome/Edge terlebih dahulu, lalu ulangi proses impor. |
| Penggunaan RAM meningkat saat banyak profil terbuka | Buka halaman **Aktivitas** atau gunakan **Tutup Sesi Lain** / aktifkan **Auto-Sleep 30m** agar sesi latar belakang yang tidak dipakai dilepaskan. |
| Website meminta login ulang setelah restore backup di PC lain | Normal: cookie sesi browser terikat pada enkripsi perangkat asal dan sengaja tidak dipindahkan lintas komputer demi keamanan. |
| Salah satu tab bermasalah (*renderer crash*) | Klik tombol **Muat Ulang** pada banner pemulihan di dalam tab tersebut tanpa perlu menutup seluruh aplikasi. |

---

## 4. Melaporkan Bug

Sertakan informasi berikut saat melaporkan masalah:
1. Versi Switchly (lihat di **Pengaturan → Tentang Switchly**) dan versi Windows 11 (`winver`).
2. Langkah singkat untuk memunculkan masalah.
3. Potongan log dari `%LOCALAPPDATA%\SwitchlyNext\Logs\` (pastikan tidak ada data pribadi sebelum dikirim).
