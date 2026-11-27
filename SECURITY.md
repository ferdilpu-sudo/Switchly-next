# Kebijakan Keamanan & Pembaruan — Switchly

**Berlaku untuk:** Switchly `0.9.0-beta.1` dan seterusnya  
**Platform yang didukung resmi:** Windows 11 x64

## 1. Arsitektur Keamanan Lokal

- **Isolasi profil ketat (`ADR-011`):** Setiap profil menggunakan direktori `CachePath` unik (direct child di bawah `%LOCALAPPDATA%\SwitchlyNext\Profiles\<profile-guid>`). Tidak ada konteks browser global yang dipakai bersama antarprofil.
- **Proteksi kredensial lokal (`ADR-005`, D6):** Password Vault (`ENT-009`) mengenkripsi kredensial per profil menggunakan **AES-256-GCM** dengan nonce CSPRNG 96-bit unik dan AAD `(CryptoVersion, CredentialId, ProfileId, Origin)`, dengan kunci utama dibungkus oleh **Windows DPAPI (`CurrentUser`)**. Tidak ada password plaintext, cookie, atau OAuth token yang disimpan di SQLite atau log.
- **Autofill eksplisit:** Tidak ada JavaScript bridge native tanpa batas. Autofill hanya berjalan ketika pengguna mengklik tombol autofill secara eksplisit dan origin halaman cocok dengan kredensial.
- **Backup terenkripsi portabel (`ADR-006`, D3):** Backup manual menggunakan streaming chunked **AES-256-GCM** dengan derivasi kunci dari passphrase pengguna (termasuk pembungkusan ulang kunci Password Vault untuk pemulihan lintas perangkat). Cookie Chromium yang terikat perangkat (`os_crypt` DPAPI) dikecualikan saat restore lintas perangkat.
- **Verifikasi lisensi offline-first (`D16`):** Mode Free (maks. 5 profil) berjalan 100% lokal tanpa login atau koneksi server. Untuk pengguna Trial Pro 7 hari / Pro, token lisensi ditandatangani secara kriptografis (**Ed25519**) oleh `Switchly.LicenseApi`, diverifikasi secara lokal menggunakan kunci publik read-only, dan disimpan terenkripsi dengan **Windows DPAPI (`CurrentUser`)** di `Data\license.dpapi`.
- **Non-elevated runtime:** Build Release menolak berjalan sebagai Administrator (CEF exit code 38) untuk menjaga batas sandbox proses Chromium.

## 2. Kebijakan Pembaruan Keamanan (CEF & .NET)

Karena Switchly menyematkan runtime Chromium melalui CefSharp/CEF:
1. **Severity Critical / High (zero-day Chromium / CEF / .NET):** Evaluasi pembaruan paket `CefSharp.Wpf.NETCore` dan `.NET 10` dalam **72 jam** sejak rilis patch upstream, lakukan build & uji regresi, lalu terbitkan rilis patch Beta/GA.
2. **Pembaruan rutin:** Sinkronkan versi CEF dan .NET 10 LTS secara berkala setiap rilis minor stabil dan perbarui `THIRD-PARTY-NOTICES.md`.
3. **Verifikasi integritas rilis:** Paket distribusi melalui **Microsoft Store (`9PH5FF24TKZR`)** ditandatangani otomatis oleh Microsoft; paket Portable ZIP / Inno Setup disertai `SHA256SUMS.txt`. Backend `Switchly.LicenseApi` diperbarui mengikuti patch keamanan `.NET 10` dan `PostgreSQL 16`.

## 3. Pelaporan Kerentanan

Jika Anda menemukan celah keamanan (misalnya kebocoran lintas profil, paparan kredensial di log/DB, atau celah restore backup):
- **Jangan** mempublikasikan detail eksploitasi secara terbuka sebelum perbaikan tersedia.
- **Jangan** menyertakan password asli, cookie sesi, atau profil pribadi dalam laporan. Gunakan log ter-redaksi dari `%LOCALAPPDATA%\SwitchlyNext\Logs\`.
- Laporkan langsung ke pengelola proyek melalui kanal privat repositori (GitHub Security Advisory / kontak maintainer).
