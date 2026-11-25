# Pemberitahuan Privasi — Switchly (Beta)

**Terakhir diperbarui:** 2026-11-25

## 1. Prinsip Local-First

Switchly dirancang sebagai aplikasi desktop **local-first**:
- **Tanpa pendaftaran akun:** Semua fitur Beta dapat digunakan secara offline tanpa membuat akun atau login ke server Switchly (`ADR-009`, D8).
- **Telemetri OFF:** Switchly **tidak** mengirimkan telemetri, analitik penggunaan, riwayat penjelajahan, daftar profil, maupun laporan crash otomatis ke server mana pun (`ADR-007`, D8).

## 2. Data yang Disimpan di Perangkat Anda

Seluruh data aplikasi disimpan secara lokal di folder akun Windows Anda (`%LOCALAPPDATA%\SwitchlyNext\`):
- `Data\switchly-next.db` — metadata profil, grup, bookmark, riwayat, unduhan, sesi tab, serta blob kredensial Password Vault yang telah dienkripsi AEAD+DPAPI.
- `Profiles\<profile-guid>\` — penyimpanan browser terisolasi milik CEF/Chromium per profil (cookie, localStorage, IndexedDB, cache).
- `Logs\` — log diagnostik lokal bergulir (*rolling logs*) yang secara default meredaksi password, cookie, token, query string URL, dan jalur file pribadi.

## 3. Impor Browser & Backup

- **Impor Chrome/Edge:** Hanya membaca secara *read-only* bookmark, riwayat, dan URL tab terbuka. Cookie dan sesi login browser sumber **tidak pernah** diimpor (`ADR-010`). Impor password hanya dilakukan jika Anda secara eksplisit memilih file CSV untuk dimasukkan ke Password Vault terenkripsi.
- **Backup Terenkripsi:** File backup manual dienkripsi secara lokal menggunakan passphrase Anda. Pengelola Switchly tidak memiliki salinan passphrase maupun kemampuan memulihkan backup jika passphrase hilang.

## 4. Penghapusan Data

- Menghapus profil dari dalam aplikasi akan menghapus metadata dan folder penyimpanan profil setelah semua handle browser ditutup.
- Menghapus aplikasi (*uninstall*) **tidak** otomatis menghapus `%LOCALAPPDATA%\SwitchlyNext\` agar data profil tidak hilang saat upgrade. Untuk menghapus seluruh data lokal secara permanen, hapus folder `%LOCALAPPDATA%\SwitchlyNext\` secara manual setelah menutup Switchly.
