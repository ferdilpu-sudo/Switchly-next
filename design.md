# UX and Interaction Design — Switchly Next

**Status:** DRAFT 0.1 — wireframe behavior specification; visual design belum final  
**Platform:** Windows desktop, mouse + keyboard, high-DPI aware

## 1. Prinsip pengalaman

1. **Profile-first:** profil dan grup menjadi struktur utama; tab/jendela adalah sesi kerja.
2. **Switch without logout:** berpindah profil tidak mengakhiri sesi profil lain.
3. **Clear separation:** nama/warna/ikon membedakan profil, tetapi penanda visual bukan pengganti isolasi teknis.
4. **Progressive resource use:** profil tersimpan tidak harus berjalan di background.
5. **Safe by default:** import kredensial, ekstensi, restore, dan penghapusan selalu menjelaskan dampak.
6. **No forced registration:** fitur Free dapat dipakai tanpa akun produk.

## 2. Information architecture

```text
Switchly Next
├── Workspace
│   ├── Favorites
│   ├── All Profiles
│   ├── Groups (manual)
│   ├── Websites (auto grouping)
│   └── Search / Filters
├── Browser Area
│   ├── Tabs
│   ├── Address Bar
│   ├── Navigation Controls
│   ├── Bookmarks
│   ├── Downloads
│   └── History
├── Profile Details
│   ├── General
│   ├── Startup URL
│   ├── Extensions
│   ├── Security
│   └── Storage
└── App Settings
    ├── Backup & Restore
    ├── Import
    ├── Privacy
    ├── Appearance
    ├── Resource Management
    └── License / Upgrade (Pro details TBD)
```

## 3. Katalog layar dan jendela

| ID | Layar | Tujuan | Elemen utama | Requirement |
|---|---|---|---|---|
| UI-001 | Main Workspace | Mengelola profil dan grup | Sidebar, search, group list, active windows | REQ-001, REQ-004 |
| UI-002 | Profile Create/Edit | Membuat/menyetel profil | Name, icon, color, group, startup URL | REQ-002, REQ-015 |
| UI-003 | Browser Window | Menjelajah web pada satu profil | Tabs, address bar, back/forward, reload, menu | REQ-001, REQ-005 |
| UI-004 | Profile Context Menu | Aksi profil | Open, close, duplicate config, archive, delete | REQ-002 |
| UI-005 | Bookmark Manager | Menata bookmark per profil | Tree/list, add/edit/delete, folder | REQ-006 |
| UI-006 | History View | Mencari riwayat profil | Filter waktu/domain, clear history | REQ-006 |
| UI-007 | Downloads Panel | Melihat dan mengelola unduhan | Progress, destination, open folder, cancel | REQ-006 |
| UI-008 | Optional Extension Settings | Mengelola ekstensi bila fitur kelak disetujui | Only runtime-supported sources and permissions | REQ-007 |
| UI-009 | Import Wizard | Mengimpor data browser | Source, detected profiles, categories, preview, result | REQ-008, REQ-009 |
| UI-010 | Security & Unlock | Membuka aplikasi/vault | Windows account state, master password, Windows Hello | REQ-010 |
| UI-011 | Backup & Restore | Melindungi/pindah data | Schedule, destination, passphrase, preview, progress | REQ-011 |
| UI-012 | Settings | Preferensi aplikasi | Startup, appearance, resource, privacy | REQ-017, REQ-018 |
| UI-013 | License / Pro | Upgrade dan status entitlements | Sign-in/activation, plan, status | REQ-012, REQ-013 |

## 4. Interaksi kritis

### 4.1 Profile sidebar
- Profil ditampilkan dengan nama, ikon, warna dan indikator status: closed, active, suspended, error.
- Klik profil:
  - Jika sudah aktif: fokuskan jendela profil terakhir.
  - Jika tertutup: buat request context dari direktori profil dan buka workspace.
  - Jika suspended: bangunkan sesuai kebutuhan dan pulihkan tab.
- Context menu memuat open, close, edit, duplicate settings, archive, delete.
- Penghapusan profil memerlukan konfirmasi, menjelaskan data yang akan hilang dan opsi backup terlebih dahulu.

### 4.2 Tabs dan windows
- Tab dalam satu profil menggunakan request context yang sama.
- Jendela tambahan untuk profil yang sama berbagi sesi yang sama.
- Profil berbeda tidak berbagi request context.
- Drag tab antarjendela hanya diperbolehkan dalam profil yang sama.
- Drag tab ke profil lain tidak memindahkan storage; tampilkan pilihan **Open URL in profile...** sehingga konten dibuka sebagai navigasi baru.
- Tab yang dipulihkan adalah URL dan metadata navigasi yang aman; form sensitif atau POST body tidak dipulihkan.

### 4.3 Import Wizard
1. Pilih Chrome atau Edge.
2. Deteksi instalasi/profil lokal; pengguna memilih sumber secara eksplisit.
3. Tampilkan status: browser harus ditutup bila database sedang terkunci.
4. Checklist:
   - Bookmark
   - Riwayat
   - Daftar ekstensi sebagai informasi opsional; reinstall bukan bagian MVP
   - Tab terbuka (URL saja)
   - Password CSV (file yang dipilih manual)
5. Jelaskan bahwa cookies dan sesi login tidak dibawa.
6. Preview jumlah entri dan duplikat sebelum commit.
7. Hasil: jumlah berhasil, dilewati, gagal, dan alasan.
8. Password CSV diurai di memori; tidak masuk log; simpan hanya ke vault terenkripsi profil setelah konfirmasi. Tampilkan peringatan bahwa CSV sumber berisi plaintext.

### 4.4 Optional extension settings (not MVP)
- Extension management is deferred and is not part of the first release gate.
- If later approved, only runtime-supported sources and APIs may be offered.
- Show source, permissions, and known compatibility limitations before installation.
- Never silently sideload or claim success when the engine reports failure.

### 4.5 Backup/restore
- Backup dapat dijalankan manual atau terjadwal.
- Aplikasi meminta passphrase dan mengonfirmasi lokasi file.
- Progress dapat dibatalkan sebelum finalisasi; file sementara dibersihkan best-effort.
- Restore menunjukkan versi arsip, jumlah profil, ukuran perkiraan, dan tanggal.
- Restore tidak menimpa data aktif tanpa konfirmasi; gunakan staged restore dan atomic swap.
- Passphrase salah, corrupt archive, ruang disk tidak cukup, dan file yang tidak dikenal memiliki error state yang jelas.

## 5. Status UI wajib

Semua layar yang melakukan operasi data harus memiliki:
- Initial/loading.
- Empty state dengan langkah berikutnya.
- Success state.
- Error state yang dapat dipahami.
- Permission denied / unsupported state.
- Busy/disabled state saat proses tidak dapat digandakan.
- Confirmation dialog untuk aksi destruktif.

## 6. Aksesibilitas dan desktop quality

- Navigasi utama dapat digunakan dengan keyboard.
- Focus ring terlihat; fokus tidak hilang ketika tab/jendela berubah.
- Shortcut umum: Ctrl+L (address bar), Ctrl+T (tab baru), Ctrl+W (tutup tab), Ctrl+Tab (tab berikutnya), Ctrl+Shift+T (restore tab) — shortcut khusus aplikasi harus menghindari benturan dengan website.
- Semua tombol icon memiliki accessible name/tooltip.
- High DPI dan scaling 100–200% diuji.
- Ukuran jendela, sidebar collapse, dan multi-monitor diperhatikan.
- Kontras teks dan status tidak hanya dibedakan dengan warna.
- Bahasa UI MVP: Bahasa Indonesia; localization framework dipakai agar bahasa lain dapat ditambahkan. `Terbuka`: kebutuhan English UI.

## 7. Arah visual (belum final)

- Desktop productivity app, tampilan rapi dan padat namun tidak sesak.
- Sidebar navigasi tetap; warna profil dapat dikustomisasi namun warna status memakai semantik konsisten.
- Browser chrome minimal: address bar, tab strip, toolbar, menu.
- Tidak memutuskan palet warna/brand sebelum pengguna menyetujuinya.
- Dark/light theme: `Asumsi` dukung system theme dan pilihan manual, perlu validasi.

## 8. Perilaku resource pada UI

- Menampilkan jumlah profil tersimpan dan jumlah yang sedang aktif.
- Profil yang tertutup tidak memegang renderer/browser process.
- Ketika memori tinggi, beri peringatan non-modal dan saran menutup/suspend tab.
- Jangan otomatis menutup tab atau kehilangan form tanpa persetujuan.
- Pengguna dapat memilih startup behavior: restore last session atau open workspace only.

## 9. Terbuka

- Branding, palet, ikon, dan design tokens: owner product sebelum beta.
- Ekstensi opsional dan bukan bagian UI MVP; evaluasi hanya jika diprioritaskan ulang.
- Apakah "semua profil aktif" berarti dapat dibuka sekaligus sebagai windows meskipun melebihi resource: perilaku harus adaptif, tidak menjanjikan kapasitas tak terbatas.
- Free/Pro upgrade screen: menunggu aturan paket dan provider lisensi.
