# Panduan Codec Proprietary CEF (H.264 / MP4 / AAC) untuk Switchly

## Mengapa Diperlukan?
Paket NuGet resmi `CefSharp.Wpf.NETCore` (`156.0.300`) menggunakan biner CEF standar (`chromiumembeddedframework.runtime.win-x64` `156.0.30`) yang dikompilasi dengan `proprietary_codecs=false` karena pembatasan lisensi paten MPEG-LA.

- **Berjalan tanpa overlay:** VP8, VP9, AV1, WebM, Opus (mis. sebagian besar video YouTube).
- **Membutuhkan overlay proprietary codecs:** MP4 (`video/mp4`), H.264 (`avc1`), AAC (`mp4a`) yang dipakai oleh **Google Flow, SeaArt, X, Instagram, TikTok**, dll.

## Versi ABI yang Harus Cocok Persis
Biner `libcef.dll` yang dipakai sebagai overlay **wajib** cocok persis dengan versi CEF yang di-pin oleh `CefSharp.Wpf.NETCore 156.0.300`:

- **FileVersion:** `156.0.30+g6028375+chromium-156.0.8078.96`
- **CEF Branch:** `8078`
- **CEF Commit:** `6028375`
- **Arsitektur:** `win-x64`

> **Keamanan:** Jangan pernah mengunduh `libcef.dll` acak dari situs/forum tidak terpercaya karena `libcef.dll` berjalan di dalam proses browser yang mengakses sesi login semua profil Anda. Selalu kompilasi dari sumber resmi CEF/Chromium.

## Cara Build & Memasang Overlay (Reproducible)

### 1. Jalankan GitHub Actions Workflow
Repo ini sudah menyertakan workflow siap pakai di `.github/workflows/build-cef-proprietary.yml`.
1. Buka tab **Actions** di repositori GitHub Anda.
2. Pilih workflow **Build CEF 156.0.30 (Proprietary Codecs win-x64)**.
3. Klik **Run workflow** (branch `8078` dan commit `6028375` sudah terisi default).
   - *Catatan:* Kompilasi Chromium/CEF membutuhkan runner Windows x64 dengan RAM >= 16 GB dan disk kosong >= 150 GB (direkomendasikan self-hosted Windows runner atau GitHub Larger Runner).
4. Setelah selesai, unduh artifact **`cef-proprietary-win-x64-156.0.30`**.

### 2. Ekstrak ke Folder Overlay Lokal
Ekstrak isi artifact (`libcef.dll`, `chrome_elf.dll`, dll.) ke:
```text
third_party/cef-proprietary/win-x64/
```
Folder `third_party/cef-proprietary/` sudah di-ignore di `.gitignore` agar file biner besar tidak pernah masuk ke commit git.

### 3. Verifikasi FileVersion & Build Switchly
Cek versi `libcef.dll` sebelum build:
```powershell
(Get-Item .\third_party\cef-proprietary\win-x64\libcef.dll).VersionInfo.FileVersion
# Harus persis: 156.0.30+g6028375+chromium-156.0.8078.96
```

Lalu jalankan build normal:
```powershell
dotnet build .\Switchly.sln -c Release --no-restore
```
MSBuild di `src/Switchly.App/Switchly.App.csproj` akan otomatis mendeteksi `third_party/cef-proprietary/win-x64/libcef.dll` dan menampilkan log:
```text
[Switchly] Applied proprietary-codec CEF overlay to ...
```
Hal yang sama juga berjalan otomatis saat `dotnet publish` / pembuatan paket rilis.

### 4. Uji Pemutaran Video
1. Jalankan `Switchly.App.exe` dan buka salah satu profil.
2. Buka Google Flow / SeaArt atau halaman uji `https://html5test.co` dan pastikan **H.264 / MPEG-4 / AAC** berstatus aktif.
3. Klik tombol **Fullscreen** pada video player — jendela Switchly akan otomatis menyembunyikan sidebar, tab bar, toolbar, dan footer hingga keluar dari fullscreen (`Esc`).

## Catatan Lisensi Distribusi Publik (D13)
Sebelum mendistribusikan biner yang memuat codec H.264/AAC secara komersial atau dalam skala publik luas, pastikan kepatuhan lisensi paten MPEG-LA / Via LA sesuai model distribusi Switchly.
