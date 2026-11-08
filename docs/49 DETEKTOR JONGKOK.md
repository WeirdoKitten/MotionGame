# 49 — Detektor jongkok (SquatDetector) dan Pick the Fruit

Detektor gerak dasar **jongkok** untuk Motion Game, dan permainan **Pick the Fruit** yang memakainya. Alasan memilih jongkok ada di [48 GERAKAN DASAR BARU.md](48%20GERAKAN%20DASAR%20BARU.md).

| File | Isi |
|---|---|
| `Scripts/MotionGame/BasicMove.cs` | `IBasicMoveDetector` (API umum detektor gerak dasar) dan `MoveRecord` (satu gerakan selesai beserta waktunya). |
| `Scripts/MotionGame/SquatDetector.cs` | Detektor jongkok. Tidak bergantung pada UI; hanya menerima `PoseResult` dan waktu. |
| `Scripts/MotionGame/PickFruitGame.cs` | Permainan ke-4. Hanya menampilkan hasil detektor. |
| `Scripts/MotionGame/PoseTracker.cs` | `PoseResult` diperluas menjadi 21 titik (`PoseJoint`). |
| `Scripts/MotionGame/MotionSensor.cs` | `InjectPose` dan `PoseSamples`. |
| `Tests/PlayMode/SquatDetectorTests.cs`, `PickFruitPlayTests.cs`, `FakePose.cs` | Test dengan pose palsu. |

## 1. Data pose

`PoseResult` lama tetap ada (`limbs`, `seen`, `hips`, `found`, `milliseconds`), jadi tiga game lama tidak berubah. Tambahannya:

- `points[21]`, `visibility[21]` (0..1), `depth[21]`, `hasDepth`, `aspect` (lebar/tinggi gambar kamera).
- Indeks memakai `PoseJoint` (urutan COCO, lalu tumit dan ujung kaki). Nama `Joint` tidak dipakai karena bentrok dengan `UnityEngine.Joint`.

| PoseJoint | BlazePose (33) | MoveNet (17) |
|---|---|---|
| Nose, LeftEye, RightEye, LeftEar, RightEar | 0, 2, 5, 7, 8 | 0–4 |
| LeftShoulder … RightWrist | 11–16 | 5–10 |
| LeftHip … RightAnkle | 23–28 | 11–16 |
| LeftHeel, RightHeel, LeftToe, RightToe | 29–32 | tidak ada (visibility 0) |

- **BlazePose:** visibility = sigmoid(min(visibility, presence)). Nilai mentah dari model adalah logit; MediaPipe juga memakai sigmoid. Depth = z landmark (piksel crop) dikali skala crop lalu dibagi lebar gambar, jadi satuannya sama dengan u. Nilai lebih kecil berarti lebih dekat ke kamera.
- **MoveNet:** visibility = skor keypoint. Tidak ada depth.
- `MotionSensor.InjectPose(pose)` memasukkan satu sampel pose seolah-olah dari model (untuk test atau data rekaman). `pose.found = false` dihitung sebagai frame hilang. `PoseSamples` bertambah setiap ada hasil pose baru, sehingga detektor memproses tiap sampel tepat sekali.

## 2. Cara kerja

### Ukuran yang dipakai

Semua jarak adalah jarak vertikal dalam koordinat layar ternormalisasi (v ke atas), dibagi **panjang kaki** saat berdiri. Dengan begitu anak yang dekat dan yang jauh dari kamera diperlakukan sama.

```
lantai     = pergelangan kaki yang lebih rendah (kaki yang diangkat tidak menggeser lantai)
kaki (L)   = pinggul_berdiri − lantai_berdiri
kedalaman  = (L − (pinggul − lantai)) / L
```

Kedalaman 0 = berdiri, ±0,25 = setengah jongkok (kuda-kuda), ±0,5 = paha sejajar lantai, ±0,7 = jongkok penuh.

Sudut lutut 2D sengaja tidak dipakai. Saat anak menghadap kamera, paha mengarah ke kamera sehingga terlihat memendek, dan sudut lutut di gambar tetap hampir lurus walau anak jongkok dalam.

### Baseline (kalibrasi)

- Selama anak diam di dalam garis panduan (5 detik) dan selama hitung mundur, `PickFruitGame` mengirim setiap sampel pose ke `SquatDetector.Calibrate`. Kalau anak bergerak dan hitungan diam mulai ulang, baseline juga diulang.
- Baseline = rata-rata tinggi pinggul, lantai, tinggi lutut di atas lantai, dan tinggi bahu. Minimal 6 sampel.
- Kalau pergelangan kaki tidak terlihat di sebagian besar sampel (kaki terpotong frame), panjang kaki diperkirakan 1,3 × panjang badan (bahu–pinggul), dan lantai dianggap tetap di posisi perkiraan itu. Kepercayaan turun (×0,6).
- Kalau `Step` dipanggil tanpa baseline, sampel-sampel pertama dianggap posisi berdiri.
- Saat berdiri diam (kedalaman ≤0,05, kecepatan ≤0,15/s, tidak ada penolakan), baseline mengikuti perubahan lambat (laju 0,1 per sampel), misalnya anak maju sedikit. Kalau kaki baru terlihat setelah kalibrasi, lantai pindah ke pergelangan kaki.

### Penyaringan

- Median dari 3 sampel terakhir menghapus lonjakan satu frame (misalnya titik pinggul meloncat ke lutut).
- `Direction` dari kecepatan kedalaman: +1 turun (>0,25/s), −1 naik (<−0,25/s), 0 diam.

### State machine

```
Waiting --(baseline siap)--> Up
Up --(kedalaman ≥ 0,10, sampel sah)--> Lowering         (mulai satu jongkok)
Lowering --(kedalaman ≥ 0,25, sah, belum rusak)--> Low   (cukup dalam; di game: buah terambil)
Low/Lowering --(kedalaman < 0,10)--> Up
      Low, tidak rusak, durasi ≥ 0,6 s  -> Count + 1, Last = MoveRecord
      selain itu, puncak ≥ 0,15          -> Incomplete + 1, LastMiss = alasan
semua --(tidak ada sampel sah > 0,6 s)--> Lost           (jongkok yang sedang berjalan dibatalkan)
Lost --(sampel sah dengan kedalaman < 0,10)--> Up         (mulai lagi dari berdiri)
```

Satu jongkok hanya dihitung sekali: anak harus kembali berdiri (kedalaman <0,10) sebelum jongkok berikutnya bisa dihitung. Naik-turun di bawah garis tidak menambah hitungan.

### Gerakan mirip yang ditolak

Sampel yang tidak sah dua kali berturut-turut selama satu jongkok membuat jongkok itu "rusak" dan tidak dihitung.

| Alasan (`Reason`/`LastMiss`) | Aturan | Gerakan |
|---|---|---|
| kurang dalam | puncak 0,15–0,25 | setengah jongkok yang terlalu dangkal |
| terlalu cepat | turun-naik < 0,6 s | memantul, tolakan lompat |
| kaki terangkat | pergelangan kaki tertinggi > lantai + 0,15 L | lompat, angkat kaki |
| lutut di lantai | lutut < 0,5 × tinggi lutut berdiri | berlutut |
| duduk | kedalaman > 0,88 | duduk di lantai |
| membungkuk | bahu turun > 0,2 L lebih banyak dari pinggul, kedalaman < 0,10 | membungkuk (tidak pernah memulai jongkok); di game diberi isyarat "Tekuk lututmu" |

Jinjit tidak perlu aturan khusus. Seluruh badan naik bersama lantai, jadi kedalaman tetap ±0.

## 3. Ambang batas

Semua bisa diubah lewat field publik `SquatDetector`.

| Field | Nilai | Arti | Pada 2 m, kamera 320x240 |
|---|---|---|---|
| `TargetDepth` | 0,25 L | jongkok dihitung (setengah jongkok) | ±13 cm, ±16 px |
| `StandDepth` | 0,10 L | batas berdiri / mulai jongkok | ±5 cm, ±6 px |
| `AttemptDepth` | 0,15 L | di bawah ini tidak dihitung "tidak lengkap" | ±8 cm |
| `FloorTolerance` | 0,15 L | kaki boleh naik sebanyak ini | ±8 cm |
| `KneeFloor` | 0,5 | lutut minimal setengah tinggi berdiri | |
| `SitDepth` | 0,88 L | lebih rendah = duduk | |
| `LegFromTorso` | 1,3 | panjang kaki dari panjang badan bila kaki tak terlihat | |
| `GapTolerance` | 0,6 s | frame hilang yang masih ditoleransi (±7 sampel) | |
| `MinSquatTime` | 0,6 s | jongkok tercepat yang dihitung | |
| `BaselineSamples` | 6 | sampel minimal baseline | ±0,5 s |

Perkiraan piksel memakai panjang kaki anak ±52 cm dan lebar gambar ±2,5 m pada jarak 2 m. Nilai-nilai ini titik awal dan perlu disetel dengan data kamera nyata (lihat bagian 8).

## 4. API detektor (`IBasicMoveDetector`)

| Anggota | Jongkok |
|---|---|
| `Step(pose, time)` | satu sampel pose baru |
| `Wait(time)` | dipanggil di antara sampel; menandai `Lost` kalau badan tidak terlihat > 0,6 s |
| `Tracking` | badan terlihat dan baseline siap |
| `Direction` | +1 turun, −1 naik, 0 diam |
| `Amount` | kedalaman sekarang 0..1 (`Depth` tanpa clamp) |
| `Peak` | kedalaman terdalam pada jongkok sekarang/terakhir |
| `Count` / `Incomplete` | jongkok selesai / dimulai tapi tidak dihitung |
| `Progress` | 0..0,5 saat turun menuju target, 0,5..1 saat naik kembali |
| `Confidence` | visibility pinggul × 0,6 bila kaki tak terlihat × 0,85 bila lutut tak terlihat (dihaluskan) |
| `Last` | `MoveRecord`: nama, waktu mulai, waktu selesai, puncak, kepercayaan terendah |

Sejak docs/50, interface ini juga memuat `HasBaseline`, `Calibrate` dan `ClearBaseline`, dan game berbasis detektor memakai class dasar `PoseMoveGame`.

Tambahan khusus jongkok: `State`, `Reason`, `LastMiss`, `Bending`, `FeetSeen`, `FloorFromFeet`, `LegLength`, `FloorHeight`, `StandingHipHeight`, `TargetHipHeight` (untuk menggambar garis target).

### Pola yang sama untuk tiga game lama (belum di-refactor)

| Game | Amount | Direction | Count | Progress | Confidence |
|---|---|---|---|---|---|
| Catch the Star (menjangkau) | jarak tangan/kaki ke target ÷ panjang lengan | – | target tersentuh | 1 − jarak ÷ jangkauan | visibility pergelangan |
| Follow the Line (menelusuri) | simpangan dari jalur ÷ panjang lengan | arah sepanjang jalur | jalur selesai | posisi di jalur 0..1 | visibility titik yang menelusuri |
| Avoid the Rock (bergeser) | geseran pinggul dari baseline ÷ lebar bahu | −1 kiri, +1 kanan | pindah jalur | jarak ke jalur tujuan | visibility pinggul |

Ketiganya sekarang membaca `MotionSensor.Energy`/`Centroid` langsung. Untuk menyusun gerakan advance, masing-masing cukup dibungkus menjadi class `IBasicMoveDetector` yang mengeluarkan `MoveRecord`. Urutan dari rekaman ahli lalu bisa dicocokkan sebagai deret `MoveRecord` (nama gerakan, urutan, jeda waktu, besar gerakan).

## 5. Pick the Fruit

- Buah (sementara: sprite bintang berwarna merah-oranye) muncul di tanah di samping kaki, bergantian kiri dan kanan, setinggi lantai hasil kalibrasi.
- Garis putih = tinggi pinggul saat setengah jongkok (`TargetHipHeight`). Bintang hijau = pinggul anak. Garis menjadi emas saat pinggul sudah di bawahnya.
- Jongkok sampai garis → buah terambil dan ikut bergerak bersama pinggul ("Berdiri, masukkan ke keranjang!"). Berdiri → buah masuk keranjang, +1 bintang.
- Isyarat ringan saat meleset: "Turun sedikit lagi!", "Pelan-pelan saja!", "Kakinya tetap di lantai ya!", "Jongkok saja, lututnya jangan di lantai!", "Tekuk lututmu, badan tetap tegak!", dan "Mundur sedikit, seluruh badan harus terlihat!" saat badan hilang > 1 s. Tidak ada game over dan tidak ada nilai.
- Keamanan: layar kalibrasi menampilkan "Main di tempat yang lapang, jauh dari meja dan kursi. Lantainya jangan licin ya!". Buah berikutnya muncul 1,2 s setelah anak berdiri, dan ada istirahat 4 s setiap 6 buah. Dengan tempo biasa (±2 s per jongkok) satu ronde 60 detik berisi ±13 jongkok; dengan tempo tercepat yang masih dihitung (0,6 s) paling banyak ±26. Jongkok dalam tidak diwajibkan; setengah jongkok sudah dihitung.
- **Tanpa model pose** (Deteksi: Gerak kamera, kamera tidak ada, atau model gagal dimuat): gerak di buah = buah terambil; gerak di area kepala ±0,6 s kemudian = berdiri. Ini hanya cadangan supaya game tetap bisa dimainkan dan diuji dengan mouse. Cadangan ini tidak bisa membedakan jongkok dari membungkuk atau melambai, dan overlay debug menandainya "cadangan".

### Overlay debug (data TA)

Tekan **F3** saat bermain untuk menyalakan/mematikan. Bawaannya mati, jadi anak tidak melihatnya. Isi: model dan waktu inferensi, fase, arah, kedalaman, puncak, target, progres, kepercayaan, jumlah, tidak lengkap, alasan meleset terakhir, alasan tolak, status kaki, panjang kaki, tinggi lantai, tinggi pinggul berdiri, dan tinggi garis. `MotionGame.ShowDebug` juga bisa diatur dari kode. Game lain bisa menampilkan nilainya sendiri dengan meng-override `DebugText`.

## 6. Keterbatasan per model

**BlazePose (bawaan)**

- Paling lengkap: punya tumit dan ujung kaki, visibility per titik, dan z. Ujung kaki belum dipakai detektor jongkok, tetapi dibutuhkan detektor lompat nanti.
- Di CPU: ±43–46 ms per frame kalau detektor orang ikut jalan, ±21 ms saat badan dilacak dari frame sebelumnya (lihat docs/50 bagian 1). Inferensi berjalan di main thread setiap 0,08 s, jadi frame sedikit tersendat.
- Detektor orangnya berbasis wajah, tetapi sekarang hanya dipakai saat badan hilang. Kalau anak menunduk dalam-dalam sambil jongkok, pelacakan tetap jalan; gap pendek ditutupi `GapTolerance`.
- Dengan sigmoid, foto uji handstand memberi 19 dari 21 titik terlihat.

**MoveNet Lightning**

- ±16 ms per frame, jadi cocok untuk perangkat lemah. Pinggul, lutut, dan pergelangan kaki tersedia, jadi detektor jongkok bekerja sama persis.
- Tidak ada tumit, ujung kaki, dan z.
- Input 192x192 dari seluruh gambar (tanpa crop orang). Anak yang jauh (>2,5 m) menjadi kecil dan titiknya lebih bergoyang.
- Lemah pada pose tidak biasa (foto handstand: hanya 2 titik). Pada jongkok menghadap kamera seharusnya masih baik, tetapi perlu dibuktikan dengan kamera.

**Tanpa pose (gerak kamera / mouse)**

- Frame differencing hanya tahu ada gerak dan di mana, bukan tinggi pinggul. Saat anak diam di bawah, gambarnya sama dengan diam berdiri.
- Cadangan "sentuh buah lalu gerak di kepala" adalah tebakan kasar dengan kepercayaan rendah. Data dari mode ini tidak layak dipakai sebagai data gerak.

**Umum**

- Laptop di atas meja sering memotong kaki. Taruh laptop lebih rendah atau anak berdiri lebih jauh (2–2,5 m). Tanpa kaki, detektor memakai perkiraan dari panjang badan.
- Hanya satu orang. Kalau orang tua ikut masuk frame, model bisa memilih orang yang salah.
- Pakaian longgar (rok, gamis, sarung) menutupi pinggul dan lutut, sehingga titiknya bergeser.
- Hanya dari depan. Jongkok dalam posisi menyamping belum diuji.
- Anak yang berjalan maju/mundur saat jongkok mengubah skala; baseline hanya menyesuaikan saat berdiri diam.

## 7. Aset yang dibutuhkan

Sementara memakai `bg-star` dan sprite `star`. Yang perlu dibuat:

- Latar kebun buah (`bg-fruit`) dan gambar kartu menu.
- Sprite buah 3–5 macam (apel, jeruk, mangga, rambutan, pisang), versi di tanah dan versi dipegang.
- Keranjang (kosong, berisi sedikit, penuh) dan efek buah masuk keranjang.
- Ikon garis target dan penanda pinggul yang lebih jelas dari bintang.
- Ikon/ilustrasi keamanan (ruang lapang, lantai tidak licin) dan ikon istirahat.
- Ilustrasi panduan jongkok (berdiri → setengah jongkok) untuk layar instruksi.
- Suara: buah dipetik, buah masuk keranjang, isyarat "turun lagi", dan suara istirahat.

## 8. Uji manual dengan kamera

Deteksi dengan kamera nyata tidak bisa diuji otomatis. Langkah:

1. Tutup Unity kalau mau menjalankan `Tools\Test-Project.ps1` dulu; setelah lulus, buka project dan scene `MotionGame`, lalu tekan Play.
2. Taruh laptop serendah mungkin (kursi/lantai) dan berdiri 2–2,5 m. Seluruh badan termasuk kaki harus terlihat. Ruang harus lapang dan lantai tidak licin.
3. Di menu, pastikan tombol bawah menunjukkan **Deteksi: BlazePose**. Pilih kartu **4 Pick the Fruit**.
4. Masuk ke garis panduan, lambaikan tangan, lalu diam 5 detik. Setelah hitung mundur, tekan **F3** untuk overlay debug.
5. Cek baseline: berdiri diam → "kedalaman" sekitar 0,00 (±0,05) dan "kaki terlihat, lantai dari kaki". Catat "kaki (v)".
6. Jongkok sampai pinggul (bintang hijau) melewati garis putih, lalu berdiri. Harapan: "jumlah" +1 tepat sekali dan bintang bertambah. Ulangi 10 kali; catat berapa yang terhitung.
7. Setengah jongkok dangkal (kedalaman puncak 0,15–0,2): tidak terhitung, "tidak lengkap" +1, isyarat "Turun sedikit lagi!".
8. Gerakan mirip, masing-masing 5 kali: membungkuk, jinjit, lompat kecil, berlutut. Harapan: "jumlah" tidak bertambah. Catat "tolak" dan "meleset terakhir".
9. Saat jongkok, tutup kamera dengan tangan sebentar (<0,5 s): masih terhitung. Keluar frame >1 s: muncul "Mundur sedikit…", lalu masuk lagi dan jongkok normal: terhitung lagi.
10. Ulangi langkah 5–8 dengan **Deteksi: MoveNet** (ganti di menu, lalu main lagi), dan sekali dengan kaki sengaja terpotong frame (laptop di meja).
11. Mode **Gerak kamera**: game tetap jalan (sentuh buah di bawah, lalu gerak di area kepala); overlay menulis "cadangan".

Catat untuk TA: tingkat deteksi benar per model, kedalaman puncak rata-rata, waktu inferensi, dan kasus gerakan mirip yang lolos. Dari data itu ambang batas di bagian 3 bisa disetel.
