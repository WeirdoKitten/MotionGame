# 47 — Motion games (Misi Gerak)

Spesifikasi sumber: `docs/motion/` (GDD Star Catch, Follow the Line, Avoid the Rock).

## Alur
- Menu utama: ketuk **rumah telur** di latar → panel **Misi Gerak** dengan 4 kartu:
  1. Catch the Star, 2. Follow the Line, 3. Avoid the Rock, 4. Pick the Fruit.
- Tiap game: hitung mundur 3-2-1 → main 60 detik → layar "Hebat!" (jumlah bintang, Main lagi / Misi lain).
  Tidak ada game over; meleset hanya diberi isyarat ringan ("Coba lagi!", "Awas!").

## Deteksi gerak (`MotionSensor`)
- Unity tidak punya pelacakan pose bawaan; yang tersedia adalah `WebCamTexture`. Kamera depan dibuka
  (izin diminta), tiap frame diperkecil ke grid abu-abu 40x30 dan dibandingkan dengan frame sebelumnya
  (frame differencing). Nilai gerak per sel meluruh ~3.5/detik. Grid dicerminkan seperti cermin.
- `Energy(titik, radius)`: gerak terkuat di sekitar titik. `Centroid()`: pusat gerak (posisi badan).
- Tanpa kamera/izin: gerakan mouse/sentuhan dihitung sebagai gerak, jadi game tetap bisa dimainkan.
- Gambar kamera (dicerminkan) tampil layar penuh semi-transparan (`CameraOpacity` .55) di atas latar, di bawah bintang/batu, sehingga tangan anak tepat di posisi target.
- Batasan: bukan pelacakan tangan/lutut/lompat seperti di GDD; semua gerakan di titik target dihitung.

## Mekanik
- **Catch the Star:** bintang muncul (urutan tengah → kiri/kanan → campur → tinggi), membesar ~1,2 dtk;
  gerak di bintang (energi > .35, radius .07) = tertangkap (+1). Hilang setelah 6 dtk.
- **Follow the Line:** 5 bentuk (lurus, diagonal, zigzag, gelombang, busur) berupa titik; bintang pemandu
  maju saat ada gerak di/tepat di depannya (koridor .08). Garis selesai = +1, bentuk berikutnya.
- **Avoid the Rock:** 3 jalur. Posisi badan = pusat gerak (dihaluskan). Batu menggelinding ke jalur
  (2,6 dtk); gelombang awal 1 batu, lalu 2 batu (selalu ada 1 jalur aman). Jalur berbahaya merah,
  aman hijau. Beda jalur saat batu tiba = +1, sama = "Awas!".

## Aset
- `Tools/Slice-Motion-Games.py`: dari `Art/MotionGamesSource/*-sheet.png` → `Resources/Motion/`
  (bg-star, bg-line, bg-rock, star, rock).

## Lainnya
- Telur 3D di halaman utama disembunyikan (area ketuknya tetap untuk menetas).
- Air terjun latar utama: dipendekkan agar berhenti di buih (tidak mengenai jembatan) dan condong ~10° ke kiri
  mengikuti gambar (`WaterFlow.Fall.slant`).
- Tes: `MotionGamesPlayTests` (ketuk rumah, menu, ketiga game dengan gerak disuntikkan).

## Kalibrasi dan tangan/kaki (update)

- Sebelum main (dan setiap Main lagi): panduan badan tampil. Anak harus **terdeteksi masuk** (gerak di area panduan, `PresenceMotion`), lalu **diam 5 detik** (bar hijau). Gerak apa pun mengulang hitungan (`StillLimit`). Tanpa keduanya game tidak pernah mulai (layar kosong/diam tidak dihitung).
- Game 1 dan 2 hanya menghitung **tangan dan kaki**: gerak di area badan+kepala (`BodyZone`) diabaikan (`MotionSensor.Ignore`). Bintang hanya muncul di samping/atas (tangan) dan bawah (kaki); garis berada di sisi kiri/kanan, lantai (kaki) dan melengkung di atas kepala.
- Batasan: ini perkiraan berdasarkan posisi berdiri, bukan pelacakan sendi. Untuk pelacakan tangan/kaki sungguhan perlu Unity Sentis + model pose (belum dipasang).
- Game 3 tetap memakai gerak seluruh badan.

## Pelacakan pose (Unity Inference Engine)

- Package `com.unity.ai.inference` 2.6.1 (nama baru Unity Sentis). Model di `Resources/Motion/Pose/`:
  - BlazePose (Google MediaPipe, dari `unity/inference-engine-blaze-pose`, Apache-2.0): `pose_detection.onnx`
    (15,1 MB) + `pose_landmarks_lite.onnx` (5,5 MB) + `anchors.csv`. 33 titik; dipakai ujung jari telunjuk dan
    ujung kaki.
  - MoveNet Lightning (Google, dari `Xenova/movenet-singlepose-lightning`, Apache-2.0): `movenet_lightning.onnx`
    (9,4 MB), input int 192x192. 17 titik; dipakai pergelangan tangan dan mata kaki.
- `PoseTracker`: gambar kamera disiapkan di CPU (affine + bilinear), model dijalankan di backend CPU tiap 0,08 dtk.
- `MotionSensor`: kalau pose ditemukan (≤0,5 dtk lalu), `Energy` = apakah tangan/kaki ada di titik itu
  (jangkauan longgar), `Centroid` = pinggul. Kalau tidak, kembali ke deteksi gerak kamera.
- Menu Misi Gerak punya tombol **Deteksi: BlazePose / MoveNet / Gerak kamera** untuk membandingkan.
  Di dalam game: bintang kuning di tangan, biru di kaki; pojok kanan bawah menampilkan model dan waktu proses.
- Perbandingan otomatis: `PoseModelTests` → `Builds/pose-model-comparison.md` (foto contoh handstand).
  Hasil: BlazePose menemukan tangan (bawah) dan kaki (atas) dengan benar, ~19 ms/frame; MoveNet lebih cepat
  (~8 ms/frame) tetapi pada pose terbalik titik-titiknya bertumpuk dengan skor rendah dan tangan tidak terdeteksi.
  Rekomendasi: **BlazePose** sebagai bawaan; MoveNet untuk perangkat lemah. Perlu diuji dengan anak berdiri di depan
  webcam sungguhan.

## Pick the Fruit dan detektor gerak dasar (update)

- Permainan ke-4 melatih satu gerakan dasar: **jongkok**. Alasan pemilihan: `docs/48 GERAKAN DASAR BARU.md`. Detail detektor, ambang batas, keterbatasan, dan langkah uji kamera: `docs/49 DETEKTOR JONGKOK.md`.
- `PoseResult` sekarang juga menyimpan 21 titik (`PoseJoint`: wajah, bahu, siku, pergelangan, pinggul, lutut, pergelangan kaki, tumit, ujung kaki) dengan visibility 0..1 dan z (BlazePose). Data lama (`limbs`, `seen`, `hips`) tidak berubah, jadi tiga game pertama tetap sama.
- Detektor dipisah dari tampilan: `SquatDetector` mengikuti `IBasicMoveDetector` (arah, besar gerakan, jumlah, progres, kepercayaan, `MoveRecord`), agar bisa dirangkai menjadi gerakan advance nanti.
- `MotionGame` mendapat tiga hook opsional: `Standing()` (dipanggil saat anak diam di panduan dan saat hitung mundur, untuk baseline), `Safety` (catatan keamanan di layar kalibrasi), dan `DebugText` (overlay debug, F3, bawaan mati).
- `MotionSensor.InjectPose` memasukkan pose buatan (untuk test), mirip `Inject` untuk gerak. `PoseSamples` menghitung hasil pose baru.
- Kartu menu dibagi rata untuk 4 kartu; judul mengecil otomatis bila perlu.
- Aset khusus belum ada: memakai `bg-star` dan sprite `star` berwarna oranye sebagai buah. Daftar aset yang dibutuhkan ada di `docs/49`.
- Tes: `SquatDetectorTests`, `PickFruitPlayTests` (pose buatan dari `FakePose`).

## Spin the Windmill, Jump the Puddle, Find the Bird (update)

- Tiga gerakan dasar lagi, satu game per gerakan: putar lengan (`ArmCircleDetector`, kartu 5), lompat (`JumpDetector`, kartu 6), memutar badan (`TurnDetector`, kartu 7). Detail: `docs/50 DETEKTOR LENGAN LOMPAT PUTAR.md`.
- `PoseMoveGame`: class dasar game berbasis satu detektor (baseline saat diam, satu sampel pose sekali, cadangan tanpa pose).
- BlazePose melacak badan antar frame dari landmark bantu 33/34 dan pose flag; detektor orang (berbasis wajah) hanya jalan saat badan hilang. ±21 ms per frame di laptop uji (sebelumnya ±45 ms).
- Menu 7 kartu: grid 4 + 3.
- Keamanan: lompat 3 per set lalu istirahat 5 s; putaran penuh paling banyak 2 per ronde, bergantian arah, lalu istirahat 5 s; putar lengan ganti lengan setiap 3 lingkaran.
- Tes: `MoveDetectorTests`, `NewGamesPlayTests`.
