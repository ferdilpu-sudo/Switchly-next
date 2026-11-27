# Pemberitahuan Privasi — Switchly

**Terakhir diperbarui:** 2026-11-26

## 1. Prinsip Local-First

Switchly dirancang sebagai aplikasi desktop **local-first**:
- **Mode Free tanpa pendaftaran:** Fitur dasar (hingga 5 profil pada rilis publik, atau seluruh fitur pada masa Beta) berjalan 100% secara lokal dan offline tanpa membuat akun atau mengirim data ke server Switchly.
- **Telemetri NONAKTIF:** Switchly **tidak** mengirimkan telemetri, analitik penggunaan, riwayat penjelajahan, daftar profil, cookies, maupun laporan crash otomatis ke server mana pun.

## 2. Data yang Disimpan di Perangkat Anda

Seluruh data browsing dan pengaturan Anda disimpan secara lokal di akun Windows Anda (`%LOCALAPPDATA%\SwitchlyNext\`):
- `Data\switchly-next.db` — metadata profil, grup, bookmark, riwayat, unduhan, sesi tab, serta data Password Vault yang dienkripsi lokal menggunakan keamanan bawaan Windows (DPAPI) dan AES-256-GCM.
- `Data\license.dpapi` *(opsional, hanya jika masuk akun)* — token lisensi lokal yang dienkripsi dengan keamanan akun Windows Anda.
- `Profiles\<profile-guid>\` — folder data browser yang terisolasi per profil (cookie, localStorage, IndexedDB, cache).
- `Logs\` — log diagnostik lokal yang secara default menyamarkan password, cookie, token, query string URL, dan jalur file pribadi.

## 3. Data Akun & Lisensi Opsional (Hanya untuk Trial Pro 7 Hari & Pro)

Jika Anda memilih membuat akun untuk mengaktifkan **Trial Pro 7 hari** atau membeli paket **Switchly Pro**, data minimal berikut diproses oleh server lisensi Switchly:
- **Alamat email** (untuk verifikasi kode OTP dan kepemilikan lisensi).
- **Sidik jari perangkat satu arah (`device_hash` — SHA-256)** untuk mencegah penyalahgunaan Trial 7 hari dan menghitung batas maksimal perangkat aktif (2 PC Windows). Nilai ini bukan nomor seri mentah dan tidak dapat dibalik menjadi informasi pribadi.
- **Status transaksi pembayaran** dari penyedia pembayaran (Midtrans) — Switchly tidak pernah melihat atau menyimpan nomor kartu kredit/PIN rekening Anda.
- **Yang TIDAK pernah dikirim ke server:** daftar profil, URL yang dikunjungi, cookie, riwayat browsing, bookmark, isi Password Vault, maupun file backup Anda.

## 4. Impor Browser & Backup Terenkripsi

- **Impor Chrome/Edge:** Hanya membaca secara *read-only* bookmark, riwayat, dan URL tab terbuka. Cookie dan sesi login browser sumber **tidak pernah** diimpor. Impor password hanya dilakukan jika Anda secara eksplisit memilih file CSV untuk dimasukkan ke Password Vault terenkripsi.
- **Backup Terenkripsi:** File backup manual dienkripsi secara lokal menggunakan passphrase pilihan Anda. Pengelola Switchly tidak memiliki salinan passphrase dan tidak dapat memulihkan backup jika passphrase hilang.

## 5. Penghapusan Data

- Menghapus profil di dalam aplikasi akan menghapus metadata serta folder data browser profil tersebut secara permanen setelah sesi ditutup.
- **Installer Standar / Portable ZIP:** Menghapus aplikasi (*uninstall*) **tidak** otomatis menghapus `%LOCALAPPDATA%\SwitchlyNext\` agar data profil tidak hilang saat pembaruan. Untuk menghapus seluruh data lokal secara permanen, hapus folder `%LOCALAPPDATA%\SwitchlyNext\` secara manual setelah menutup Switchly.
- **Paket Microsoft Store (MSIX):** Berdasarkan perilaku standar kontainer aplikasi Windows Store, menghapus instalasi (*uninstall*) aplikasi Microsoft Store akan **ikut menghapus folder data lokal tervirtualisasi milik paket tersebut**. Pastikan Anda mengekspor cadangan terenkripsi (`.snbak`) terlebih dahulu sebelum meng-uninstall versi Microsoft Store jika ingin menyimpan data profil dan Password Vault Anda.
