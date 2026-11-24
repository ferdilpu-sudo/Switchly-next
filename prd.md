# Product Requirements Document — Switchly Next

**Status:** ACTIVE (0.2) — M-001 engine PoC & provider sign-in validated on Windows 11; M-002 desktop foundation in progress; scope refined per PO decisions D1–D5 (2026-11-24)  
**Version:** 0.2  
**Tanggal:** 24 November 2026  
**Nama produk:** Switchly Next (nama kerja; branding belum dikonfirmasi)  
**Pemilik produk:** Pengguna / product owner

## 1. Masalah dan tujuan

Pengguna yang mengelola banyak akun website mengalami kerepotan karena harus logout-login berulang kali atau membuka banyak profil/jendela browser secara manual. Switchly Next menyediakan satu aplikasi desktop Windows untuk menyimpan, mengelompokkan, membuka, dan menggunakan banyak profil browser dengan sesi yang terisolasi.

**Hasil yang dituju:** pengguna dapat berpindah dari satu profil akun ke profil lain tanpa harus logout dari profil sebelumnya, sementara cookies, local storage, cache, bookmark, riwayat, dan sesi setiap profil tidak tercampur (termasuk data ekstensi bila fitur ekstensi opsional kelak diaktifkan).

## 2. Pengguna dan konteks

### Persona utama — pengguna multi-akun
- Mengelola banyak akun pada berbagai website.
- Ingin mengorganisasi profil berdasarkan layanan atau kelompok kerja.
- Memerlukan tab dan jendela paralel.
- Menginginkan data lokal dan kontrol backup/restore.
- Tidak wajib membuat akun produk untuk menggunakan fitur dasar.

### Persona sekunder — pelanggan Pro
- Menggunakan fitur gratis terlebih dahulu.
- Memerlukan fitur premium setelah paket Pro ditetapkan.
- Membuat akun/aktivasi hanya saat membeli atau mengaktifkan fitur Pro.

**Model distribusi:** produk desktop Windows untuk publik. Model bisnis freemium telah dipilih; batas Free/Pro, harga, pembayaran, dan server lisensi masih `Terbuka`.

## 3. Sasaran produk

**Layanan prioritas untuk uji kompatibilitas awal:** Google Flow (https://flow.google), ChatGPT (https://chatgpt.com), Dola (https://dola.com), dan SeaArt (https://seaart.ai). Daftar ini adalah target pengujian, bukan jaminan bahwa setiap layanan menerima embedded browser. Ketentuan autentikasi, usia, dan penggunaan dari masing-masing penyedia tetap berlaku.


1. Memisahkan sesi browsing antarprofil secara nyata.
2. Mengurangi langkah berpindah akun menjadi memilih profil yang diinginkan.
3. Menyediakan pengelolaan banyak profil dan grup.
4. Memungkinkan sejumlah profil aktif bersamaan sesuai resource perangkat, tanpa batas angka tetap yang dipaksakan.
5. Memindahkan data browser yang dipilih dari Chrome/Edge dengan langkah yang jelas dan aman.
6. Menjaga data utama di komputer pengguna dan menyediakan backup/restore yang terenkripsi.

## 4. Batas MVP

### 4.1 Must — MVP inti

| ID | Requirement |
|---|---|
| REQ-001 | Aplikasi berjalan sebagai aplikasi desktop Windows dengan satu workspace utama dan dukungan jendela browser terpisah. |
| REQ-002 | Pengguna dapat membuat, mengubah, menggandakan konfigurasi, membuka, menutup, mengarsipkan, dan menghapus profil. Penggandaan tidak boleh menyalin sesi rahasia secara diam-diam. |
| REQ-003 | Setiap profil memiliki penyimpanan browser persisten yang terisolasi dari profil lain. |
| REQ-004 | Pengguna dapat mengelompokkan profil secara manual dan melihat grup otomatis berdasarkan website/domain yang dikenali. |
| REQ-005 | Pengguna dapat membuka beberapa profil dan jendela/tab bersamaan; sistem mengelola resource tanpa kuota aktif tetap. |
| REQ-006 | Setiap profil memiliki bookmark, riwayat, tab/session restore, dan download manager yang terikat pada profil tersebut. |
| REQ-008 | Aplikasi menyediakan wizard impor dari Chrome dan Edge untuk bookmark, riwayat, dan tab terbuka sebagai URL. Daftar ekstensi boleh ditampilkan sebagai informasi opsional; pemasangan ulang bukan syarat MVP. Impor password CSV dan custom password vault ditunda ke pasca-MVP (keputusan D1). |
| REQ-009 | Aplikasi tidak mengimpor cookies atau sesi login browser sumber pada MVP; pengguna perlu login ulang pada profil baru. |
| REQ-010 | Aplikasi menyediakan perlindungan lokal melalui akun Windows/DPAPI, serta opsi kunci lokal (master password dan Windows Hello jika tersedia). |
| REQ-011 | Aplikasi menyediakan backup **manual** dan restore dari arsip terenkripsi (chunked AES-256-GCM) yang dapat dipindahkan ke komputer lain. Cookies/sesi login browser bersifat terikat perangkat (Windows DPAPI) dan tidak dipindahkan lintas komputer (konsisten dengan `REQ-009`). Backup terjadwal otomatis ditunda ke pasca-MVP (keputusan D2). |
| REQ-012 | Aplikasi dapat digunakan untuk fitur dasar tanpa registrasi atau koneksi layanan akun produk. |
| REQ-013 | Aplikasi menyediakan pengalaman Free/Pro melalui batas entitlement yang dapat dikembangkan, tetapi detail paket dan mekanisme pembayaran belum menjadi bagian MVP sampai diputuskan (`M-009`). |
| REQ-014 | Aplikasi mendukung Windows 11 sebagai target utama dan memvalidasi kompatibilitas Windows 10 sesuai matriks dukungan dalam dokumen ini. |

> *Catatan penomoran:* `REQ-007` (ekstensi opsional) berada di bagian `4.2 Should / Deferred`.

### 4.2 Should — setelah alur inti stabil (atau pasca-MVP)

- `REQ-007`: Dukungan ekstensi Chromium dasar per profil boleh ditambahkan jika versi CEF yang dipilih mendukungnya secara stabil. Ekstensi bukan prioritas MVP, bukan syarat rilis awal, dan tidak ada janji kompatibilitas penuh Chrome Web Store.
- `REQ-015`: Pengguna dapat mengatur profil favorit, warna, ikon, dan website awal.
- `REQ-016`: Pengguna dapat mencari dan memfilter profil, tab, bookmark, dan grup (profil & grup pada `M-003`; tab & bookmark pada `M-004`).
- `REQ-017`: *(Ditunda pasca-MVP — D2)* Pengguna dapat mengonfigurasi jadwal backup otomatis dan retensi setelah desain enkripsi terjadwal (hybrid public-key) disetujui.
- `REQ-018`: Aplikasi dapat menampilkan indikator resource sederhana untuk profil/jendela aktif.

### 4.3 Di luar cakupan MVP

- Sinkronisasi cloud atas sesi/profile data.
- Impor cookies atau sesi login dari Chrome/Edge.
- Impor password CSV & custom password vault lokal (`ENT-009`, ditunda pasca-MVP per D1).
- Backup terjadwal otomatis tanpa interaksi pengguna (`REQ-017`, ditunda pasca-MVP per D2).
- Pemindahan cookies/sesi login browser lintas perangkat saat restore backup (terikat DPAPI perangkat asal, D3).
- Sinkronisasi password ke layanan cloud.
- Otomatisasi massal aktivitas akun, botting, scraping, atau upaya menghindari pembatasan website.
- Mengakali CAPTCHA, pembatasan layanan, autentikasi, atau sistem anti-abuse.
- Mobile/macOS/Linux.
- Sinkronisasi real-time lintas perangkat.
- Marketplace ekstensi milik Switchly.
- Pembayaran dan backend lisensi sampai desain Free/Pro dikunci.
- Klaim kompatibilitas penuh terhadap semua ekstensi Chrome atau semua website.

## 5. Alur pengguna utama

### FLOW-001 — Membuat profil baru
1. Pengguna menekan **New Profile**.
2. Mengisi nama, ikon/warna opsional, grup, serta URL awal opsional.
3. Aplikasi membuat `profile_id` dan direktori data browser khusus.
4. Browser membuka sesi kosong untuk profil itu.
5. Pengguna login ke website secara normal.
6. Pada penggunaan berikutnya, sesi profil tersebut dipulihkan jika website masih mengizinkannya.

### FLOW-002 — Berpindah akun
1. Pengguna memilih profil lain dari sidebar/search.
2. Aplikasi membuka atau memfokuskan jendela/tab profil yang dipilih.
3. Profil sebelumnya tetap tersimpan; tidak perlu logout.
4. Tidak ada cookies/local storage lintas profil yang terbagi.

### FLOW-003 — Impor Chrome/Edge
1. Pengguna memilih browser sumber dan profil sumber.
2. Aplikasi memeriksa ketersediaan dan meminta browser sumber ditutup bila perlu.
3. Pengguna memilih kategori data: bookmark, riwayat, tab terbuka (URL saja), dan opsional daftar ekstensi (informasi saja, bukan reinstall). *(Impor password CSV ditunda pasca-MVP — D1.)*
4. Aplikasi menampilkan preview, jumlah item, dan peringatan format.
5. Pengguna memilih profil tujuan atau membuat profil baru.
6. Aplikasi mengimpor data nonrahasia yang didukung.
7. Cookies/sesi login tidak dipindahkan.

### FLOW-004 — Backup manual dan restore
1. Pengguna menjalankan backup manual. *(Backup terjadwal otomatis ditunda pasca-MVP — D2.)*
2. Aplikasi memastikan profil target ditutup/di-flush dan melakukan checkpoint SQLite; direktori `<profile-guid>` disalin tanpa CEF global state maupun cache disposable.
3. Pengguna memasukkan passphrase backup.
4. Aplikasi mengenkripsi arsip (chunked streaming AES-256-GCM) dan memvalidasi integritas.
5. Pada restore, aplikasi memverifikasi arsip dan menampilkan isi sebelum menulis.
6. Restore dijalankan ke staging directory; setelah validasi lulus, aplikasi melakukan **restart-to-restore staging swap** sebelum CEF/SQLite membuka handle. Cookies/sesi login terikat perangkat tidak dipindahkan lintas komputer (D3).

### FLOW-005 — Mengelola ekstensi (opsional, setelah MVP)
1. Pengguna memilih profil dan membuka pengaturan ekstensi jika fitur tersedia.
2. Aplikasi hanya menawarkan format/sumber yang didukung versi CEF yang dipaketkan.
3. Aplikasi menampilkan sumber, izin, dan keterbatasan sebelum instalasi.
4. Pengguna memberi persetujuan eksplisit.
5. Ketidakcocokan ekstensi ditampilkan secara jelas; tidak ada fallback atau klaim palsu.

## 6. Kriteria penerimaan

- `AC-001`: Aplikasi dapat dibangun dan dijalankan di Windows 11 x64; Windows 10 diuji sesuai `TEST-001` dan status dukungannya dilaporkan jelas.
- `AC-002`: Profil dapat dibuat, diubah, diduplikasi konfigurasinya, diarsipkan, dan dihapus dengan metadata serta direktori unik yang konsisten; duplikasi konfigurasi tidak boleh menyalin cookies/sesi rahasia secara diam-diam.
- `AC-003`: Login/cookie/local storage pada Profil A tidak muncul di Profil B pada uji isolasi.
- `AC-004`: Membuka profil yang sama kembali memulihkan data sesi persistennya selama website masih mempertahankan sesi.
- `AC-005`: Grup manual dan grup otomatis dapat ditampilkan tanpa mengubah kepemilikan profil.
- `AC-006`: Beberapa profil dapat dibuka dalam kombinasi tab/jendela; menutup satu profil tidak menutup profil lain.
- `AC-007`: Bookmark, riwayat, tab terbuka, dan download dapat ditampilkan/dioperasikan pada konteks profil yang benar.
- `AC-008`: Jika dukungan ekstensi diimplementasikan, ekstensi yang tidak didukung harus ditolak atau ditandai dengan alasan yang akurat. Pengujian ekstensi bukan blocker MVP dan tidak boleh menghasilkan klaim kompatibilitas Chrome Web Store secara umum.
- `AC-009`: Wizard impor menampilkan preview, menangani browser sumber yang sedang berjalan, dan tidak mengimpor cookies/sesi.
- `AC-010`: *(Ditunda pasca-MVP — D1)* Jika impor password CSV kelak diaktifkan, CSV hanya diproses setelah tindakan eksplisit pengguna, tidak ditulis ke log, dan disimpan terenkripsi per profil; file sumber tidak diklaim terhapus aman.
- `AC-011`: Backup manual dapat dibuat dan dipulihkan di komputer Windows lain dengan passphrase yang benar (metadata & data non-device-bound pulih tanpa cookies/sesi perangkat lama); passphrase salah, arsip terpotong, atau arsip termodifikasi ditolak tanpa merusak data aktif.
- `AC-012`: Pengguna dapat membuat/mengelola profil dasar tanpa akun produk.
- `AC-013`: Semua fitur yang belum masuk Free/Pro diperlakukan sebagai `Terbuka`, bukan dibatasi secara arbitrer.
- `AC-014`: Build dan smoke test dijalankan pada Windows 11 dan Windows 10 test image; status dukungan OS ditampilkan di README/release notes.
- `AC-015`: Pengguna dapat menandai profil favorit dan mengubah ikon/warna/website awal tanpa mengubah `ProfileId` atau data sesi.
- `AC-016`: Pencarian dan filter mengembalikan profil/grup (M-003) serta tab/bookmark (M-004) yang sesuai tanpa mengubah urutan atau kepemilikan data.
- `AC-017`: *(Ditunda pasca-MVP — D2)* Jika backup terjadwal kelak diaktifkan, perubahan jadwal dan retensi berlaku pada siklus terjadwal berikutnya.
- `AC-018`: Indikator resource menampilkan status profil aktif dan peringatan ketika tekanan resource melewati ambang yang telah diuji.
- `AC-019`: Perlindungan lokal (`REQ-010`) hanya menerima metode unlock terkonfigurasi (Windows account/DPAPI, master password, atau Windows Hello bila tersedia), gagal secara aman saat tidak valid, dan tidak membocorkan rahasia/kredensial/cookie/query string ke log atau laporan crash.

## 7. Non-functional requirements

### Keamanan dan privasi
- Isolasi profil wajib berlaku pada cookie store, local storage, IndexedDB, cache, permission, service worker, dan extension data when an extension is installed; extensions remain optional.
- Semua tindakan import/export/restore yang menyentuh kredensial memerlukan persetujuan pengguna yang jelas.
- Rahasia tidak boleh masuk ke log, crash dump yang tidak terkontrol, analytics, atau telemetry.
- Telemetry non-esensial: `Asumsi` OFF secara default; keputusan akhir masih terbuka.
- Backup lintas perangkat harus dienkripsi sebelum ditulis ke file final.
- Aplikasi tidak boleh menyimpan password dalam plaintext di SQLite.

### Performa
- `NFR-PERF-001`: Profil yang tersimpan tetapi tidak dibuka tidak boleh membuat proses renderer/browser aktif.
- `NFR-PERF-002`: Profil nonaktif harus dapat tetap tersimpan di disk tanpa mempertahankan seluruh tab di RAM.
- `NFR-PERF-003`: Tidak menetapkan angka RAM universal sebelum pengukuran; M-001 membuat baseline pada perangkat uji minimum dan perangkat menengah.
- `NFR-PERF-004`: Penutupan/proses suspend profil harus mem-flush data dan membebaskan resource sesuai kemampuan CEF.
- Jumlah profil aktif tidak diberi hard cap; aplikasi boleh memberi peringatan dan menawarkan penutupan/suspend berdasarkan tekanan resource.

### Kompatibilitas
- Windows 11 x64: target dukungan utama.
- Windows 10: target kompatibilitas sesuai permintaan, tetapi edisi dan status dukungan vendor OS/runtime harus dijelaskan. Windows 10 Home/Pro telah melewati akhir dukungan reguler; .NET 10 secara resmi hanya mencantumkan Windows 10 Enterprise/LTSC. Windows 10 Home/Pro adalah `best-effort` sampai keputusan rilis final.
- Chromium/CEF harus diperbarui berkala untuk patch keamanan.

## 8. Asumsi dan hal terbuka

| Item | Status | Asumsi sementara | Dampak / pemilik |
|---|---|---|---|
| Nama Switchly Next | Asumsi | Nama kerja saja | Product owner menetapkan branding sebelum beta. |
| CEF runtime | Dikonfirmasi (`ADR-003`) | CEF Chrome Runtime (satu-satunya mode sejak CEF 128) melalui `CefSharp.Wpf.NETCore 156.0.300`; **API ekstensi** Chrome Runtime bukan prioritas | Tervalidasi pada M-001 + uji sign-in manual Windows 11; uji Windows 10 & WPF non-elevated tetap dilacak di M-002/M-008. |
| Ekstensi Chromium | Dikonfirmasi bukan prioritas | Tidak menjadi syarat MVP; dukungan terbatas dapat dievaluasi setelah browser inti stabil | Product owner dapat menentukan cakupan opsional setelah ada bukti kompatibilitas. |
| Windows 10 Home/Pro | Terbuka (`OPEN-002`) | Best-effort compatibility | Tetapkan dukungan resmi sebelum rilis publik. |
| Free vs Pro | Terbuka (`OPEN-001`) | Semua fitur dasar berjalan tanpa registrasi; fitur premium belum dibatasi | Tentukan benefit, limit, harga, backend, dan refund/support policy. |
| Login Pro | Dikonfirmasi (`ADR-009`) | Akun diperlukan hanya untuk pembelian/aktivasi fitur Pro | Provider/auth dan lisensi belum dipilih (`OPEN-005`). |
| Backup otomatis | Ditunda pasca-MVP (D2, `OPEN-006`) | MVP backup manual saja; backup terjadwal butuh desain hybrid public-key | Dievaluasi ulang setelah MVP stabil. |
| Password vault & CSV import | Ditunda pasca-MVP (D1, `OPEN-009`) | Tidak masuk MVP; login normal lewat sesi profil Chromium | Menghindari konflik `AGENTS.md` dan permukaan serangan password manager di MVP. |
| Cookies/session import & restore lintas PC | Di luar cakupan (`ADR-010`, D3) | Tidak diimpor dan tidak dibawa lintas komputer | Pengguna login ulang pada profil baru/komputer baru. |

## 9. Hubungan dokumen

- UX dan perilaku layar: [design.md](./design.md)
- Struktur komponen dan teknologi: [architecture.md](./architecture.md)
- Data dan kontrak: [schema.md](./schema.md)
- Keputusan teknis: [decisions.md](./decisions.md)
- Aturan implementasi: [rules.md](./rules.md)
- Pengujian dan bukti penerimaan: [testing.md](./testing.md)
- Tahapan pengerjaan: [plan.md](./plan.md)
- Orientasi repo: [README.md](./README.md)
- Instruksi coding agent: [AGENTS.md](./AGENTS.md)
