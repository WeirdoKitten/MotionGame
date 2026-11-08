# 50 — Detektor putar lengan, lompat, dan memutar badan

Lanjutan dari [48 GERAKAN DASAR BARU.md](48%20GERAKAN%20DASAR%20BARU.md) dan [49 DETEKTOR JONGKOK.md](49%20DETEKTOR%20JONGKOK.md). Tiga gerakan dasar lagi, masing-masing dengan satu detektor dan satu permainan:

| Gerakan dasar | Detektor | Permainan | Kartu |
|---|---|---|---|
| Putar lengan (arm circle) | `ArmCircleDetector` | Spin the Windmill (`WindmillGame`) | 5 |
| Lompat (jump) | `JumpDetector` | Jump the Puddle (`PuddleGame`) | 6 |
| Memutar badan (body turn) | `TurnDetector` | Find the Bird (`BirdGame`) | 7 |

Putar pinggang tidak dibuat. Di gambar 2D, twist 30° hanya membuat bahu ±13% lebih sempit (setara noise), dan MoveNet tidak bisa membedakan arah twist. Gerakan ini baru masuk akal kalau world landmarks BlazePose (output index 4, 3D) sudah dibaca.

## 1. Bagian bersama

- `IBasicMoveDetector` sekarang juga memuat `HasBaseline`, `Calibrate(pose, time)`, dan `ClearBaseline()`, karena semua detektor butuh posisi berdiri. `MoveRecord` mendapat `direction`.
- `PoseMoveGame` (di `BasicMove.cs`) adalah class dasar untuk game dengan satu detektor:
  - mengirim sampel ke `Calibrate` selama anak diam di garis panduan dan selama hitung mundur (baseline diulang kalau anak bergerak);
  - `Feed()` mengirim setiap sampel pose baru tepat sekali (`Step`), atau `Wait` kalau tidak ada sampel baru;
  - `UsingPose` bernilai false kalau tidak ada model pose (gerak kamera atau mouse), dan game memakai cadangannya sendiri;
  - `DebugHeader` menyediakan baris pertama overlay F3 (arah, besar, puncak, progres, kepercayaan, jumlah).
  - Pick the Fruit juga sudah memakai class ini.
- Menu misi sekarang 7 kartu dalam grid (4 di atas, 3 di bawah, baris bawah di tengah).

### Pelacakan BlazePose antar frame

Kode lama menjalankan detektor orang BlazePose (berbasis wajah) di setiap frame. Sekarang `BlazePoseTracker` mengikuti cara MediaPipe:

1. Frame pertama: detektor orang → kotak badan → landmark.
2. Frame berikutnya: kotak badan diambil dari landmark bantu frame sebelumnya (33 = pusat pinggul, 34 = titik ukuran dan kemiringan), tanpa detektor.
3. Output kedua model landmark (`Identity_1`, pose flag) menyatakan apakah badan masih ada. Nilainya sudah probabilitas (0,9995 pada foto uji). Kalau < 0,5, badan dianggap hilang dan detektor orang dijalankan lagi di frame yang sama.

Hasilnya:

- Waktu per frame di laptop uji turun dari ±43–46 ms menjadi ±21 ms, karena detektor orang hanya jalan saat badan hilang.
- Anak yang membelakangi kamera atau kepalanya keluar frame (saat melompat) tetap dilacak.
- `PoseResult.presence` dan `PoseResult.tracked` tersedia untuk data TA. Overlay menulis "(lacak)" saat sedang melacak.
- `PoseTracker.TrackBetweenFrames = false` mengembalikan perilaku lama, untuk perbandingan.

## 2. Putar lengan — `ArmCircleDetector`

**Cara kerja.** Untuk tiap lengan, dihitung sudut pergelangan tangan terhadap bahu dalam proporsi gambar asli (u dikali `aspect`). Selisih sudut antar sampel di-unwrap (`Mathf.DeltaAngle`) lalu dijumlahkan. Jumlah ±360° = satu lingkaran; sisanya dibawa ke lingkaran berikutnya. Arah: +1 = berlawanan jarum jam di layar (layar seperti cermin), −1 = searah jarum jam.

Panjang lengan diukur saat kalibrasi (berdiri, lengan tergantung): jarak pergelangan–bahu, minimal 0,8 × panjang badan.

| Field | Nilai | Arti |
|---|---|---|
| `MinRadius` | 0,45 × panjang lengan | lebih dekat dari ini sudutnya tidak dipakai (siku ditekuk, lengan mengarah ke kamera) |
| `MaxStep` | 150° | lompatan sudut antar sampel yang lebih besar dianggap ambigu dan dilewati |
| `StallTime` | 1,5 s | tidak ada kemajuan selama ini = lingkaran berakhir |
| `AttemptAngle` | 180° | lingkaran yang berhenti setelah ≥180° dihitung tidak lengkap ("berhenti") |
| `GapTolerance` | 0,6 s | frame hilang yang masih ditoleransi |

Pada 12 sampel per detik, `MaxStep` 150° berarti lingkaran sampai ±2,5 putaran per detik masih terbaca (test: 2 putaran/detik).

**Tidak terhitung:** setengah lingkaran bolak-balik, melambai, lingkaran kecil (siku ditekuk atau tangan berputar di samping), lingkaran ke samping badan (di kamera hanya terlihat naik-turun), berhenti di tengah, dan badan hilang > 0,6 s.

**Spin the Windmill.**

- Kincir (sementara: sprite bintang berwarna) berdiri di bahu target, dikelilingi cincin titik. Bintang kuning berlari di cincin untuk menunjukkan arah, dan kincir berputar mengikuti sudut lengan.
- Satu lingkaran ke arah yang benar = 1 bintang. Setiap 3 lingkaran ganti set dengan istirahat 2,5 s. Urutan set: kanan searah jarum jam (keluar), kiri berlawanan (keluar), kanan berlawanan (ke dalam), kiri searah (ke dalam).
- Arah terbalik → "Arahnya sebaliknya ya, ikuti bintang kuning!". Lengan lain → "Pakai lengan kanan ya!". Tidak ada bintang, tidak ada hukuman.
- Tanpa model pose: gerak terkuat di cincin sekitar bahu panduan dijadikan pergelangan palsu dan dimasukkan ke detektor yang sama (`Fallback`). Bisa dimainkan dengan mouse (gambar lingkaran di sekitar kincir). Kepercayaannya rendah.

**Keterbatasan per model.**

- Kedua model punya pergelangan dan bahu.
- Lingkaran harus di bidang depan (menghadap kamera). Lingkaran pemanasan ke samping tidak terbaca tanpa z.
- Putaran cepat membuat pergelangan blur dan titiknya hilang.
- Lengan yang melintas di depan wajah bisa membuat BlazePose kehilangan wajah sesaat; pelacakan antar frame mengurangi ini.

## 3. Lompat — `JumpDetector`

**Cara kerja.** Baseline sama seperti jongkok: tinggi pinggul, lantai (pergelangan kaki yang lebih rendah), lantai ujung kaki, dan panjang kaki L. Satu lompatan:

1. **Lepas landas:** kedua pergelangan kaki ≥ 0,06 L di atas lantai, pinggul ≥ 0,05 L di atas tinggi berdiri, kedua ujung kaki ≥ 0,03 L (kalau model punya ujung kaki), selisih tinggi kedua kaki ≤ 0,2 L, **dan** naik cepat dari lantai (≥ 0,6 L/s sejak sampel terakhir yang masih di lantai).
2. **Di udara:** sampel boleh hilang (kepala keluar frame). Batas waktu di udara 0,7 s.
3. **Mendarat:** pergelangan kaki terendah kembali < 0,03 L → `Count` + 1. Tinggi lompatan = tinggi pinggul tertinggi (`Last.peak`, dalam L).

Tanpa kaki di frame, hanya lompatan yang jelas dihitung (pinggul ≥ 0,1 L, L diperkirakan 1,3 × panjang badan), dengan kepercayaan × 0,5.

Kepercayaan per lompatan: × 0,7 kalau ada sampel hilang di udara, × 0,8 kalau model tidak punya ujung kaki (MoveNet).

| Field | Nilai | ±cm (L ≈ 52 cm) |
|---|---|---|
| `FeetLift` | 0,06 L | 3 cm |
| `HipLift` | 0,05 L | 2,6 cm |
| `ToeLift` | 0,03 L | 1,6 cm |
| `FeetApart` | 0,2 L | 10 cm |
| `RiseSpeed` | 0,6 L/s | 31 cm/s |
| `LandLift` | 0,03 L | 1,6 cm |
| `MaxAir` | 0,7 s | |

**Tidak terhitung:** jinjit (ujung kaki tetap di lantai, atau naiknya lambat), angkat satu kaki ("satu kaki"), jongkok, naik ke sesuatu lalu diam di atas ("terlalu lama"), dan goyangan saat berdiri.

**Jump the Puddle.**

- Genangan (sprite batu berwarna biru, dipipihkan) meluncur 2,2 s dari ujung jalan ke kaki. Saat sampai muncul "Lompat!".
- Lompatan yang mendarat dari 0,7 s sebelum sampai sampai 0,5 s sesudahnya = 1 bintang. Kalau genangan lewat tanpa lompatan: "Coba lagi!".
- Genangan datang 3 per set, lalu istirahat 5 s ("Istirahat dulu, tarik napas..."). Lompatan di luar waktu itu tidak dihitung, jadi anak tidak terdorong melompat terus. Dalam 60 detik ada ±12 genangan.
- Layar kalibrasi: "Lompat kecil saja sudah cukup! Jauh dari meja dan kursi, lantai tidak licin, jangan pakai kaus kaki licin."
- Tanpa model pose: gerak di genangan saat sampai di kaki dihitung (mouse atau gerak kaki). Ini tidak membedakan lompat dari menghentak.

**Keterbatasan per model.**

- **BlazePose:** ujung kaki membedakan jinjit dari lompat. Pelacakan antar frame menjaga badan saat kepala keluar frame.
- **MoveNet:** tidak ada ujung kaki. Jinjit cepat yang naik-turun (memantul di ujung kaki) bisa ikut terhitung. Kepercayaan ditandai lebih rendah.
- **Umum:**
  - Lompatan kecil hanya 2–3 sampel di udara pada 12 Hz.
  - Lompatan 5 cm pada 2 m hanya ±6 px.
  - Eksposur 1/30 s di dalam ruangan membuat kaki blur saat lepas landas.
  - Kaki harus terlihat untuk lompatan kecil.

## 4. Memutar badan — `TurnDetector`

**Isyarat per sampel.**

| Isyarat | Arti |
|---|---|
| Lebar bahu ÷ panjang badan, dibanding saat kalibrasi | ≥ 0,75 = depan atau belakang (sudut ≤ 41°), ≤ 0,55 = samping (≥ 57°). Satu bahu tidak terlihat = samping. |
| Wajah (hidung atau mata, visibility ≥ 0,5) | terlihat = setengah depan, tidak = setengah belakang |
| Hidung bergeser dari tengah bahu ≥ 0,12 panjang badan | arah putaran (ke kiri layar = hadap kiri anak, karena layar seperti cermin) |
| Selisih z bahu ≥ 0,1 panjang badan (BlazePose) | bahu yang lebih dekat menunjukkan arah, juga di setengah belakang |
| Hanya satu telinga terlihat | arah (cadangan) |

Hasil sampel: Front, Left, Right, Back, atau tidak tentu (sedang berputar). Sudut perkiraan (`Yaw`) juga dihitung untuk data.

**Dari sampel ke putaran.**

- Satu arah hadap dianggap "tetap" setelah bertahan 0,3 s (minimal 2 sampel). Putaran dihitung dari urutan arah hadap tetap, dalam satuan seperempat putaran (`Quarter`: depan 0, kiri −1, belakang ±2, kanan +1; tidak dibungkus).
- Kalau satu sisi terlewati (putaran cepat, frame hilang, misalnya depan → belakang), isyarat yang terlihat di antaranya menentukan jalan putarnya. Frame hilang setelah posisi samping dianggap petunjuk "belakang". Kalau tetap ambigu, arah putaran terakhir yang dipakai, dan kepercayaan × 0,5 (`Ambiguous`).
- **Putaran penuh** = kembali menghadap depan dengan `Quarter` berubah ≥ 4 sejak menghadap depan terakhir. Setengah putaran lalu kembali lewat sisi yang sama (depan → kiri → belakang → kiri → depan) bukan putaran penuh.
- Berputar ≥ 45° lalu kembali tanpa sempat tetap di samping = tidak lengkap.
- Badan hilang > 0,8 s → `Tracking` false; > 4 s → arah hadap dilupakan, dan saat terlihat lagi tidak dihitung sebagai putaran.

**Tidak terhitung:** menoleh saja (bahu tetap lebar), putaran kecil lalu kembali, dan setengah putaran bolak-balik dihitung sebagai putaran penuh.

**Find the Bird.**

- Burung (sementara: sprite bintang berwarna) hinggap di kiri atau kanan layar ("Hadap ke kiri"), terbang ke belakang ("Balik badan"), atau berputar mengelilingi layar ("Putar badan satu kali ke kiri, pelan-pelan").
- Arah hadap yang benar ditahan 0,5 s → bintang, lalu "Sekarang hadap depan lagi!". Langkah berikutnya dimulai setelah anak menghadap depan.
- Salah arah → "Ke arah burungnya ya!". Lewat 10 s → "Tidak apa-apa, nanti coba lagi!".
- Urutan tetap, 14 langkah: kiri, kanan, belakang, kanan, kiri, belakang, **putar penuh kiri**, kiri, kanan, belakang, **putar penuh kanan**, kanan, kiri, belakang.
- Keamanan:
  - Paling banyak 2 putaran penuh per ronde, tidak pernah berturut-turut, dan arahnya bergantian.
  - Setiap putaran penuh diikuti istirahat 5 s ("Istirahat dulu supaya tidak pusing").
  - Layar kalibrasi: "Putar pelan-pelan, jauh dari meja dan kursi. Kalau pusing, berhenti dan duduk dulu."
- Tanpa model pose: kamera tidak bisa melihat arah hadap. Gerak di burung (kiri/kanan) atau banyak gerak di area badan (belakang, putaran penuh) dipakai sebagai pengganti.

**Keterbatasan per model.**

- **BlazePose:** z bahu memberi arah putaran juga saat membelakangi. Pelacakan antar frame menjaga badan saat wajah hilang. Label kiri/kanan bisa tertukar dari belakang; detektor tidak bergantung pada urutan label untuk lebar bahu, dan sisi samping biasanya tetap terlihat dari hidung.
- **MoveNet:** tidak ada z. Arah di setengah belakang tidak diketahui, jadi putaran mengandalkan sisi depan (hidung, telinga). Wajah kadang "ditebak" dengan skor sedang.
- **Umum:**
  - Kalibrasi harus menghadap depan, karena lebar bahu dibanding posisi itu.
  - Rambut panjang, jilbab, atau pakaian longgar mengaburkan bahu.
  - Putaran sangat cepat melewati sisi samping.
  - Anak yang membelakangi tidak melihat layar; belum ada suara.

## 5. Overlay debug (F3)

Semua game baru menampilkan baris umum (model, waktu, "(lacak)", arah, besar, puncak, progres, kepercayaan, jumlah, tidak lengkap) ditambah:

- **Putar lengan:** target lengan dan arah, sudut kiri/kanan, jumlah lingkaran kiri/kanan, jari-jari relatif, panjang lengan, alasan meleset.
- **Lompat:** fase, tinggi pinggul, tinggi kaki, kaki/ujung kaki terlihat, alasan tolak dan meleset, tinggi dan lama lompatan terakhir, panjang kaki, lantai.
- **Memutar badan:** arah hadap tetap dan per sampel, seperempat putaran, sudut, lebar bahu relatif, wajah, arah isyarat, putaran penuh, ambigu, langkah game.

## 6. Aset yang dibutuhkan

- **Spin the Windmill:** latar ladang kincir, sprite kincir (badan + baling-baling terpisah supaya bisa diputar), panah melingkar, efek angin/daun saat satu putaran selesai, gambar kartu.
- **Jump the Puddle:** latar jalan setapak setelah hujan, sprite genangan (beberapa ukuran, tampak samping), efek cipratan, sepatu bot atau katak sebagai karakter, gambar kartu.
- **Find the Bird:** sprite burung (hinggap, terbang, dari belakang), dahan di kiri/kanan, ikon "balik badan" dan "putar", gambar kartu.
- **Semua:** ikon keamanan (ruang lapang, lantai tidak licin, berhenti kalau pusing) dan suara isyarat. Suara penting untuk Find the Bird, karena anak tidak melihat layar saat membelakangi.

## 7. Uji manual dengan kamera

Persiapan sama dengan docs/49 bagian 8: laptop serendah mungkin, berdiri 2–2,5 m, seluruh badan termasuk kaki terlihat, ruang lapang, lantai tidak licin. Pakai **Deteksi: BlazePose**, lalu ulangi dengan **MoveNet**. Tekan **F3** setelah hitung mundur.

**Spin the Windmill (kartu 5)**

1. Berdiri diam saat kalibrasi dengan lengan tergantung. Cek "panjang lengan" di overlay (seharusnya ±0,15–0,25).
2. Ikuti bintang kuning: luruskan lengan kanan dan buat lingkaran besar menghadap kamera. Harapan: tiap lingkaran +1 bintang, kincir berputar mengikuti lengan.
3. Putar ke arah sebaliknya: isyarat "Arahnya sebaliknya", tidak ada bintang.
4. Gerakan mirip: melambai, setengah lingkaran bolak-balik, lingkaran kecil di siku, dan lingkaran lengan ke samping (pemanasan biasa). Semuanya tidak boleh terhitung.
5. Catat: berapa lingkaran benar yang terhitung dari 10, dan pada kecepatan berapa mulai meleset.

**Jump the Puddle (kartu 6)**

1. Lompat kecil saat genangan sampai di kaki → bintang. Catat "lompatan terakhir" (tinggi dalam L dan lama di udara).
2. Jinjit dan tahan, jinjit memantul cepat, angkat satu kaki, jongkok: tidak boleh terhitung. Dengan MoveNet, jinjit memantul cepat mungkin terhitung; catat seberapa sering.
3. Lompatan lebih tinggi sampai kepala keluar frame: tetap terhitung (overlay "(lacak)").
4. Pastikan setelah 3 genangan ada istirahat.

**Find the Bird (kartu 7)**

1. Hadap kiri saat burung di kiri, tahan → bintang; kembali ke depan.
2. Balik badan saat diminta, tahan, kembali ke depan. Lihat overlay: "seperempat" harus kembali ke 0 dan "putaran penuh" tetap 0.
3. Putaran penuh pelan (±3 s) saat diminta → bintang, lalu istirahat 5 s.
4. Menoleh saja tanpa memutar badan: tidak boleh terhitung.
5. Ulangi dengan MoveNet. Catat berapa putaran yang "ragu" (ambigu) dan apakah arah hadap belakang terbaca.
6. Coba juga dengan `PoseTracker.TrackBetweenFrames = false`, ubah sementara di kode, untuk melihat beda pelacakan.

Catat untuk TA per gerakan dan model: tingkat terhitung benar, gerakan mirip yang lolos, waktu inferensi, dan nilai puncak (sudut, tinggi). Dari data ini ambang batas di atas bisa disetel.
