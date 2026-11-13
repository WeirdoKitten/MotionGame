# 47 — Motion games (Misi Gerak)

Spesifikasi sumber: `docs/motion/` (GDD Star Catch, Follow the Line, Avoid the Rock).

## Alur
- Menu utama: ketuk **rumah telur** di latar → panel **Misi Gerak** dengan 3 kartu:
  1. Catch the Star, 2. Follow the Line, 3. Avoid the Rock.
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

## Catch the Star: target gambar tangan dan kaki (update)

- Target bukan lagi bintang, tetapi gambar tangan (terbuka, mengepal, menyamping) atau telapak kaki (`LimbIcons`, digambar lewat kode sampai ada aset final). Gambar dicerminkan seperti kamera: target di kanan untuk tangan kanan, dengan jempol mengarah ke badan. Tangan dimiringkan menjauhi bahu.
- Warna mengikuti penanda pose: tangan kuning, kaki biru. Gambar transparan saat menunggu dan menyala saat tangan atau kaki mendekat. Saat tertangkap, gambar pecah menjadi bintang yang terbang ke penghitung bintang.
- Dengan model pose, target tangan hanya bisa ditangkap tangan dan target kaki hanya kaki (`MotionSensor.LimbEnergy`); sisi kiri/kanan tidak diwajibkan. Tanpa model pose tetap memakai gerak kamera.
- Bentuk tangan baru berupa petunjuk ("Kepalkan tangan!"). Model pose belum melihat jari, jadi yang dicek hanya posisi. Lihat docs/51.

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
