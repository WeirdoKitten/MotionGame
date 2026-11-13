# Motion Game

Versi ringan dari modul **Motion Game** pada game Family Adventure, dipisah untuk penelitian tugas akhir di bidang computer vision. Repository ini hanya berisi permainan kamera, pipeline deteksi geraknya, dan alat untuk mengubah video senam menjadi permainan; seluruh fitur lain (onboarding, buku, Companion, Sanctuary, backend, dan sebagainya) tidak disertakan agar project lebih ringan dibuka dan dijalankan.

## Kredit

Motion Game berasal dari game **Family Adventure** karya [AIKABAYAN](https://github.com/AIKABAYAN). Kode awal, aset, dan desain permainan diambil dari repository [AIKABAYAN/FamilyAdventure](https://github.com/AIKABAYAN/FamilyAdventure) pada commit `4323296`. Source lengkap game tersebut tetap berada di repository itu.

## Permainan

| Permainan | Gerakan dasar | Cara kerja |
|---|---|---|
| Catch the Star | Menjangkau (reach) | Gambar tangan (terbuka, mengepal, atau menyamping) atau telapak kaki muncul di sekitar badan; taruh tangan atau kaki di gambar itu dan gambar berubah menjadi bintang. |
| Follow the Line | Menelusuri (trace) | Bintang pemandu berjalan di sepanjang garis selama tangan atau kaki bergerak di atasnya. |
| Avoid the Rock | Bergeser (shift) | Batu datang ke satu atau dua dari tiga jalur; geser badan ke jalur yang aman. |
| Ikuti Senam | Rangkaian dari video senam: menjangkau dua tangan, angkat lutut, langkah, gerak kepala, tangan di pinggang, jalan di tempat | Gerakan instruktur dari video asli diputar dengan temponya; gambar tangan, kaki, dan wajah muncul di posisi badan anak yang sama dengan instruktur. Lihat [docs/52](docs/52%20KONVERSI%20SENAM.md). |

Setiap ronde: kalibrasi (anak berdiri di dalam garis dan diam 5 detik), hitung mundur, main 60 detik (Ikuti Senam: sepanjang video), lalu layar hasil. Tidak ada game over; meleset hanya diberi isyarat ringan.

## Dari video senam ke permainan

```powershell
python -m pip install -r Tools/requirements.txt
python Tools/Video-To-Pose.py Senam.mp4                        # pose per frame (model ONNX yang sama dengan game)
python Tools/Pose-To-Senam.py Builds/Senam/Senam-pose.csv --name "Senam Pemanasan" --video Senam.mp4
```

Hasilnya file koreografi `Assets/_Project/Resources/Motion/Senam/senam-pemanasan.txt` yang dimainkan oleh Ikuti Senam, plus laporan dan gambar cek di `Builds/Senam/` (target digambar kembali di atas video asli). Video sumber tidak ikut di repository.

## Pipeline computer vision

1. **Kamera** – `WebCamTexture` membuka kamera depan pada 320x240, 30 fps (izin diminta melalui `MobilePermissions`).
2. **Frame differencing** (`MotionSensor`) – tiap frame diperkecil menjadi grid abu-abu 40x30, dibandingkan dengan frame sebelumnya, lalu nilai gerak per sel meluruh seiring waktu. Grid dicerminkan seperti cermin.
3. **Estimasi pose** (`PoseTracker`, Unity Inference Engine, backend CPU):
   - **BlazePose** (Google MediaPipe): detektor orang + landmarker 33 titik.
   - **MoveNet Lightning** (Google): satu jaringan 17 titik, lebih cepat.
   Bila pose terdeteksi, tangan, kaki, dan pinggul dipakai langsung; frame differencing menjadi cadangan.
4. **Cadangan tanpa kamera** – gerakan mouse atau sentuhan dihitung sebagai gerak, sehingga game tetap bisa dicoba dan diuji.

Tombol **Deteksi** di menu misi berganti antara BlazePose, MoveNet, dan gerak kamera saja, untuk membandingkan ketiganya. Waktu inferensi per frame tampil di pojok kanan bawah saat bermain.

## Menjalankan

1. Pasang Unity **6000.6.0f1** melalui Unity Hub.
2. Di Unity Hub pilih **Add project from disk**, lalu pilih folder repository ini.
3. Buka scene `Assets/_Project/Scenes/MotionGame.unity` dan tekan **Play**. Menu misi langsung terbuka.

Pembukaan pertama memerlukan koneksi internet untuk mengunduh package Unity.

## Pengujian

```powershell
.\Tools\Test-Project.ps1
```

Script ini menjalankan PlayMode test di Unity batch mode:

- `MotionGameAppTests` – scene `MotionGame` membuka menu misi; kamera belum menyala sebelum misi dipilih.
- `MotionGamesPlayTests` – ketiga permainan pertama merespons gerak (memakai gerak buatan, tanpa kamera); gambar target tangan dan kaki.
- `SenamPlayTests` – koreografi hasil konversi terbaca; anak tiruan yang meniru video menyelesaikan hampir semua gerakan, anak yang diam hampir tidak ada; Ikuti Senam bisa dimainkan dengan pose buatan.
- `PoseModelTests` – BlazePose dan MoveNet dijalankan pada foto uji; hasil waktu dan posisi ditulis ke `Builds/pose-model-comparison.md`.

Hasil test ada di `Logs/playmode-results.xml`, screenshot di `Builds/Screenshots/`.

## Struktur

```text
Assets/_Project/
  Scripts/MotionGame/   MotionGames (menu + 3 permainan), SenamGame + Senam (Ikuti Senam), MotionSensor,
                        PoseTracker, LimbIcons, MotionGameApp (titik masuk), UiKit, IntroIcons, MobilePermissions
  Resources/Motion/     Latar, sprite bintang/batu, model ONNX di Pose/, koreografi senam di Senam/
  Resources/Fonts|Decor Font Sniglet (OFL) dan ornamen UI
  Art/MotionGamesSource Sheet sumber sprite dan foto uji pose
  Scenes/MotionGame.unity
  Settings/             Render pipeline (URP mobile)
  Tests/PlayMode/
docs/
  10 MOTION GAME.md     Aturan produk Motion Game
  47 MOTION GAMES.md    Catatan implementasi
  51 LATAR BELAKANG TA.md  Alasan dan tujuan tugas akhir
  52 KONVERSI SENAM.md  Video senam ke permainan Ikuti Senam
  motion/               GDD ketiga permainan dan sheet konsep aset
Tools/
  Slice-Motion-Games.py Memotong sheet sumber menjadi sprite di Resources/Motion
  Video-To-Pose.py      Video -> pose BlazePose per frame (CSV)
  Pose-To-Senam.py      Pose -> koreografi Ikuti Senam + laporan
  Senam-Check.py        Gambar cek: target di atas video asli
  Test-Project.ps1
```

## Perbedaan dengan project asli

- Menu misi dibuka langsung oleh `MotionGameApp` saat scene `MotionGame` dimuat, bukan dari rumah telur di menu utama.
- Package yang tidak dipakai Motion Game dihapus (2D animation/tilemap/spriteshape, Timeline, Visual Scripting, tutorial, dan lainnya).
- Namespace tetap `FamilyAdventure.Presentation` dan nama class tidak diubah, sehingga perbaikan di sini bisa disalin kembali ke Family Adventure.
- Identitas aplikasi: nama produk "Motion Game", package id `com.weirdokitten.motiongame`, agar bisa dipasang berdampingan dengan game lengkap.

## Aturan produk

Motion Game dimulai dengan sengaja oleh pengguna, kamera hanya aktif selama bermain, dan anak tidak diberi penilaian performa. Pengukuran akurasi untuk penelitian boleh dilakukan secara internal, tetapi tidak ditampilkan sebagai nilai kepada anak.
