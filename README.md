# Motion Game

Versi ringan dari modul **Motion Game** pada game Family Adventure, dipisah untuk penelitian tugas akhir di bidang computer vision. Repository ini hanya berisi tujuh permainan kamera dan pipeline deteksi geraknya; seluruh fitur lain (onboarding, buku, Companion, Sanctuary, backend, dan sebagainya) tidak disertakan agar project lebih ringan dibuka dan dijalankan.

## Kredit

Motion Game berasal dari game **Family Adventure** karya [AIKABAYAN](https://github.com/AIKABAYAN). Kode awal, aset, dan desain permainan diambil dari repository [AIKABAYAN/FamilyAdventure](https://github.com/AIKABAYAN/FamilyAdventure) pada commit `4323296`. Source lengkap game tersebut tetap berada di repository itu.

## Permainan

| Permainan | Gerakan dasar | Cara kerja |
|---|---|---|
| Catch the Star | Menjangkau (reach) | Bintang muncul di sekitar badan; gerakkan tangan atau kaki ke bintang untuk menangkapnya. |
| Follow the Line | Menelusuri (trace) | Bintang pemandu berjalan di sepanjang garis selama tangan atau kaki bergerak di atasnya. |
| Avoid the Rock | Bergeser (shift) | Batu datang ke satu atau dua dari tiga jalur; geser badan ke jalur yang aman. |
| Pick the Fruit | Jongkok (squat) | Buah ada di tanah; turunkan pinggul sampai garis untuk mengambilnya, lalu berdiri untuk memasukkannya ke keranjang. |
| Spin the Windmill | Putar lengan (arm circle) | Kincir di bahu; luruskan lengan dan buat lingkaran besar ke arah yang ditunjuk untuk memutarnya. |
| Jump the Puddle | Lompat (jump) | Genangan meluncur ke kaki; lompat kecil saat genangan sampai. Tiga genangan per set, lalu istirahat. |
| Find the Bird | Memutar badan (turn) | Burung hinggap di kiri/kanan, terbang ke belakang, atau berputar; putar badan menghadapnya (90°, 180°, sesekali 360°). |

Setiap ronde: kalibrasi (anak berdiri di dalam garis dan diam 5 detik), hitung mundur, main 60 detik, lalu layar hasil. Tidak ada game over; meleset hanya diberi isyarat ringan.

Gerakan dasar ini nantinya dirangkai menjadi gerakan advance (urutan tetap dari tari dan bela diri). Usulan gerakan dasar baru ada di `docs/48 GERAKAN DASAR BARU.md`; detektor jongkok di `docs/49 DETEKTOR JONGKOK.md`; detektor putar lengan, lompat, dan memutar badan di `docs/50 DETEKTOR LENGAN LOMPAT PUTAR.md`.

## Pipeline computer vision

1. **Kamera** – `WebCamTexture` membuka kamera depan pada 320x240, 30 fps (izin diminta melalui `MobilePermissions`).
2. **Frame differencing** (`MotionSensor`) – tiap frame diperkecil menjadi grid abu-abu 40x30, dibandingkan dengan frame sebelumnya, lalu nilai gerak per sel meluruh seiring waktu. Grid dicerminkan seperti cermin.
3. **Estimasi pose** (`PoseTracker`, Unity Inference Engine, backend CPU):
   - **BlazePose** (Google MediaPipe): detektor orang + landmarker 33 titik. Setelah frame pertama, badan dilacak dari landmark frame sebelumnya; detektor orang hanya dipakai lagi saat badan hilang.
   - **MoveNet Lightning** (Google): satu jaringan 17 titik, lebih cepat.
   Hasilnya disimpan di `PoseResult`: ujung tangan/kaki dan pinggul (dipakai tiga game pertama), serta 21 titik badan (`PoseJoint`) dengan visibility 0..1, dan z untuk BlazePose. Bila pose terdeteksi, titik-titik ini dipakai langsung; frame differencing menjadi cadangan.
4. **Detektor gerak dasar** (`IBasicMoveDetector`) – mengubah deret pose menjadi gerakan: arah, besar gerakan, jumlah, progres, dan kepercayaan. Ada `SquatDetector` (jongkok), `ArmCircleDetector` (putar lengan), `JumpDetector` (lompat), dan `TurnDetector` (memutar badan); masing-masing dipakai satu permainan.
5. **Cadangan tanpa kamera** – gerakan mouse atau sentuhan dihitung sebagai gerak, sehingga game tetap bisa dicoba dan diuji.

Tombol **Deteksi** di menu misi berganti antara BlazePose, MoveNet, dan gerak kamera saja, untuk membandingkan ketiganya. Waktu inferensi per frame tampil di pojok kanan bawah saat bermain. Tombol **F3** saat bermain menyalakan overlay debug berisi nilai detektor untuk data penelitian (bawaannya mati, jadi tidak terlihat oleh anak).

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
- `MotionGamesPlayTests` – menu berisi 7 kartu; tiga permainan pertama merespons gerak (memakai gerak buatan, tanpa kamera).
- `SquatDetectorTests` – detektor jongkok dengan pose palsu: jongkok benar dihitung tepat sekali, jongkok dangkal atau tidak kembali berdiri tidak dihitung, gerakan mirip (membungkuk, jinjit, lompat, berlutut) tidak dihitung, frame hilang sebentar ditoleransi, kaki di luar frame.
- `PickFruitPlayTests` – Pick the Fruit dengan pose yang disuntikkan (`MotionSensor.InjectPose`), overlay debug, dan perilaku tanpa model pose.
- `MoveDetectorTests` – detektor putar lengan, lompat, dan memutar badan dengan pose palsu: gerakan benar dihitung tepat sekali, gerakan tidak lengkap dan gerakan mirip tidak dihitung (melambai, lingkaran kecil, jinjit, angkat satu kaki, menoleh, setengah putaran bolak-balik), frame hilang sebentar ditoleransi.
- `NewGamesPlayTests` – Spin the Windmill, Jump the Puddle, dan Find the Bird dengan pose yang disuntikkan dan tanpa model pose.
- `PoseModelTests` – BlazePose dan MoveNet dijalankan pada foto uji; BlazePose harus melacak badan antar frame. Hasil waktu, posisi, jumlah titik terlihat, dan pose flag ditulis ke `Builds/pose-model-comparison.md`.

Hasil test ada di `Logs/playmode-results.xml`, screenshot di `Builds/Screenshots/`.

## Struktur

```text
Assets/_Project/
  Scripts/MotionGame/   MotionGames (menu + 3 permainan pertama), PickFruitGame, WindmillGame,
                        PuddleGame, BirdGame, MotionSensor, PoseTracker,
                        BasicMove (API detektor + PoseMoveGame), SquatDetector,
                        ArmCircleDetector, JumpDetector, TurnDetector,
                        MotionGameApp (titik masuk), UiKit, IntroIcons, MobilePermissions
  Resources/Motion/     Latar, sprite bintang/batu, model ONNX di Pose/
  Resources/Fonts|Decor Font Sniglet (OFL) dan ornamen UI
  Art/MotionGamesSource Sheet sumber sprite dan foto uji pose
  Scenes/MotionGame.unity
  Settings/             Render pipeline (URP mobile)
  Tests/PlayMode/
docs/
  10 MOTION GAME.md     Aturan produk Motion Game
  47 MOTION GAMES.md    Catatan implementasi
  48 GERAKAN DASAR BARU.md  Katalog gerakan dasar, analisis kandidat, rekomendasi
  49 DETEKTOR JONGKOK.md    SquatDetector: cara kerja, ambang batas, keterbatasan, uji manual
  50 DETEKTOR LENGAN LOMPAT PUTAR.md  Putar lengan, lompat, memutar badan; pelacakan BlazePose
  motion/               GDD tiga permainan pertama dan sheet konsep aset
Tools/
  Slice-Motion-Games.py Memotong sheet sumber menjadi sprite di Resources/Motion
  Test-Project.ps1
```

## Perbedaan dengan project asli

- Menu misi dibuka langsung oleh `MotionGameApp` saat scene `MotionGame` dimuat, bukan dari rumah telur di menu utama.
- Package yang tidak dipakai Motion Game dihapus (2D animation/tilemap/spriteshape, Timeline, Visual Scripting, tutorial, dan lainnya).
- Namespace tetap `FamilyAdventure.Presentation` dan nama class tidak diubah, sehingga perbaikan di sini bisa disalin kembali ke Family Adventure.
- Identitas aplikasi: nama produk "Motion Game", package id `com.weirdokitten.motiongame`, agar bisa dipasang berdampingan dengan game lengkap.

## Aturan produk

Motion Game dimulai dengan sengaja oleh pengguna, kamera hanya aktif selama bermain, dan anak tidak diberi penilaian performa. Pengukuran akurasi untuk penelitian boleh dilakukan secara internal, tetapi tidak ditampilkan sebagai nilai kepada anak.
