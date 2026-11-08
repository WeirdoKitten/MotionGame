# 48 — Gerakan dasar baru untuk Motion Game (usulan)

Usulan ini memilih satu gerakan dasar baru untuk Motion Game. Tujuan tugas akhir: gerakan dasar nanti dirangkai menjadi gerakan advance, yaitu urutan tetap dari tari dan bela diri yang disusun dari rekaman ahli. Dokumen ini hanya membahas gerakan dasar (satu gerakan), bukan kombinasi.

Status: **jongkok** (Pick the Fruit, [docs/49](49%20DETEKTOR%20JONGKOK.md)), lalu **putar lengan** (Spin the Windmill), **lompat** (Jump the Puddle), dan **memutar badan** 90°/180°/360° (Find the Bird) sudah diimplementasikan ([docs/50](50%20DETEKTOR%20LENGAN%20LOMPAT%20PUTAR.md)). **Putar pinggang** tidak dibuat karena tidak andal di 2D. Pelacakan BlazePose antar frame (koreksi kedua di bawah) juga sudah dibuat.

## a. Katalog gerakan dasar

Sudah ada:

| Gerakan dasar | Permainan | Yang diukur |
|---|---|---|
| Menjangkau (reach) | Catch the Star | Tangan/kaki sampai di satu titik. |
| Menelusuri (trace) | Follow the Line | Tangan/kaki mengikuti jalur. |
| Bergeser (shift) | Avoid the Rock | Badan pindah jalur kiri/kanan. |

Belum terwakili (kandidat tugas ini):

| Gerakan dasar | Variasi | Sumbu/besaran |
|---|---|---|
| Memutar badan | Hadap samping 90°, balik badan 180°, putaran penuh 360° | Arah hadap badan (sudut yaw). |
| Memutar pinggang | Twist bahu terhadap pinggul, kaki diam | Sudut bahu relatif pinggul. |
| Memutar lengan | Lingkaran tangan | Sudut pergelangan terhadap bahu, akumulasi. |
| Lompat | Dua kaki lepas dari lantai | Tinggi pinggul dan kaki di atas baseline. |
| Jongkok | Setengah jongkok sampai jongkok penuh | Turunnya pinggul relatif panjang kaki. |

Belum terwakili, di luar kandidat tugas ini (dicatat untuk katalog): angkat lutut/berdiri satu kaki, menendang, memukul/mendorong ke depan (butuh kedalaman), bertepuk, membungkuk/meliuk, melangkah maju/mundur.

## b. Analisis kandidat

Asumsi kamera: kamera laptop 320x240, anak berdiri 1,5–2 m. Pada 2 m, lebar gambar sekitar 2,5 m, jadi 1 px ≈ 0,8 cm. Bahu anak (±28 cm) ≈ 35 px. Pose diproses sekitar 12 kali per detik (`PoseInterval` 0,08 s). Di laptop pengujian BlazePose butuh ±43 ms per frame dan MoveNet ±16 ms (`Builds/pose-model-comparison.md`).

Dua koreksi untuk petunjuk teknis awal:

- **Visibility BlazePose masih logit.** MediaPipe memberi sigmoid pada visibility dan presence (`tensors_to_pose_landmarks_and_segmentation.pbtxt`: `visibility_activation: SIGMOID`). Kode lama membandingkan nilai mentah dengan 0,5; itu setara peluang ±0,62. `PoseResult.visibility` yang baru sudah memakai sigmoid (0..1). Logika lama `seen` tidak diubah.
- **Detektor orang BlazePose berbasis wajah.** MediaPipe memakai wajah sebagai sinyal utama untuk menemukan orang, lalu melacak area badan dari landmark frame sebelumnya. Kode di repo ini menjalankan detektor setiap frame tanpa pelacakan itu. Akibatnya, saat anak membelakangi kamera atau kepala keluar frame (puncak lompatan), BlazePose kemungkinan besar kehilangan seluruh badan. Ini perlu diperbaiki (pelacakan ROI dari titik bantu 33/34 dan pose flag di output index 1) sebelum membuat game memutar badan atau lompat. Output index 4 berisi world landmarks 3D (meter, titik nol di pinggul); ini cara paling langsung untuk mengukur sudut hadap.

### Memutar badan — hadap samping 90°

- **BlazePose:** rasio lebar bahu terhadap panjang badan (bahu–pinggul) turun dari ±0,6–0,8 (menghadap) ke <0,25 (samping). Hidung bergeser dari titik tengah bahu ke arah hadap. Satu telinga hilang. Nilai z: bahu yang lebih dekat ke kamera punya z lebih kecil, jadi memberi arah. World landmarks memberi sudut langsung: `atan2(dz, dx)` dari vektor bahu.
- **MoveNet:** tidak ada z. Rasio bahu dan posisi hidung tetap bisa dipakai; arah dari posisi hidung dan skor telinga. Saat samping, kedua bahu bertumpuk sehingga label kiri/kanan tidak stabil.
- **Tanpa pose:** frame differencing tidak tahu arah hadap; hanya tahu ada gerak badan besar. Mouse: geser horizontal sebagai pengganti.
- **Keandalan 320x240:** sedang–tinggi untuk 90°. Lebar bahu samping ±10 px, noise ±3 px. Sudut di bawah 45° tidak bisa dibedakan.
- **Risiko anak:** rendah.

### Memutar badan — balik badan 180°

- **BlazePose:** wajah hilang (visibility hidung/mata turun), lebar bahu kembali lebar, dan urutan bahu kiri/kanan di layar terbalik (kalau label benar). Masalah utama: detektor berbasis wajah sering tidak menemukan orang yang membelakangi (lihat koreksi di atas). Label kiri/kanan sering tertukar.
- **MoveNet:** tetap memberi titik karena tidak punya detektor terpisah, tetapi wajah sering "ditebak" dengan skor sedang dan kiri/kanan sering tertukar.
- **Tanpa pose:** tidak mungkin.
- **Keandalan:** rendah–sedang. Wajib memakai state machine berbasis waktu (depan → samping → belakang → samping → depan) yang tahan terhadap frame hilang.
- **Risiko anak:** rendah–sedang. Anak tidak melihat layar saat membelakangi, jadi butuh isyarat suara.

### Memutar badan — putaran penuh 360°

- **BlazePose/MoveNet:** state machine: depan → samping A → (belakang atau hilang) → samping B → depan, dalam batas waktu (mis. 3 s). Samping B harus berlawanan arah dengan samping A (dilihat dari posisi hidung). Setengah putaran lalu kembali (samping A → belakang → samping A) tidak dihitung.
- **Tanpa pose:** tidak mungkin.
- **Keandalan:** rendah. Putaran 1–1,5 s pada 12 Hz berarti tahap samping hanya 2–4 sampel; putaran cepat bisa melompati tahap samping.
- **Risiko anak:** tinggi (pusing, jatuh, menabrak perabot). GDD Avoid the Rock juga melarang "rapid spinning". Kalau dibuat: batasi putaran beruntun, ganti-ganti arah, beri jeda.

### Memutar pinggang

- **BlazePose:** rotasi bahu relatif pinggul dari selisih z (bahu kiri–kanan dibanding pinggul kiri–kanan) atau dari world landmarks. Di 2D, lebar bahu hanya berkurang sebesar cos θ: twist 30° hanya 13% lebih sempit, setara noise.
- **MoveNet:** hanya cos θ di 2D, arah tidak diketahui. Tidak andal.
- **Tanpa pose:** tidak mungkin.
- **Keandalan:** rendah. **Risiko anak:** rendah.

### Memutar lengan (lingkaran tangan)

- **BlazePose/MoveNet:** sudut pergelangan terhadap bahu, di-unwrap lalu diakumulasi; satu putaran = 360°, arah = tanda akumulasi. Radius minimum (mis. ≥0,5 panjang lengan) supaya jitter tidak terhitung. Lingkaran harus di bidang depan (seperti mengelap kaca). Lingkaran lengan ke samping (pemanasan biasa) terlihat sebagai garis naik-turun di kamera dan tidak terdeteksi tanpa z.
- **Tanpa pose:** cukup bisa. Frame differencing pada cincin sektor di sekitar bahu hasil kalibrasi, lalu urutan sektor yang menyala dilacak (mirip Follow the Line). Mouse: gambar lingkaran. Ini cadangan terbaik di antara semua kandidat.
- **Keandalan:** tinggi untuk lingkaran depan 1–2 putaran per detik (6–12 sampel per putaran). Di atas 3 putaran per detik terjadi aliasing dan blur.
- **Risiko anak:** sangat rendah.
- **Catatan:** paling mirip dengan menelusuri yang sudah ada (busur di Follow the Line), jadi kebaruannya paling kecil.

### Lompat

- **BlazePose:** pinggul, kedua pergelangan kaki, dan kedua ujung kaki (landmark 31/32) naik di atas baseline lalu turun, dengan profil cepat (didahului tekukan lutut, diakhiri pendaratan). Masalah: kepala keluar frame di puncak lompatan sehingga detektor berbasis wajah gagal.
- **MoveNet:** tidak ada ujung kaki, jadi jinjit dan lompatan kecil sulit dibedakan; harus bergantung pada profil waktu. Kelebihannya, MoveNet tetap jalan walau kepala terpotong.
- **Koreksi angka:** waktu melayang 0,3–0,5 s setara tinggi lompatan 11–31 cm. Lompatan kecil anak (5–10 cm) hanya melayang 0,20–0,29 s, artinya 2–3 sampel pada 12 Hz. Seluruh gerakan (tekuk–tolak–melayang–mendarat) ±0,8–1 s, jadi lintasan pinggulnya masih punya ±10 sampel. Lompatan 5 cm pada 2 m hanya ±6 px, sementara jitter titik 2–4 px. Eksposur 1/30 s di dalam ruangan menambah blur.
- **Koreksi syarat:** "kedua pergelangan kaki naik" tidak membedakan lompat dari jinjit, karena jinjit juga menaikkan kedua tumit dan pinggul ±5–8 cm. Pembedanya ujung kaki (BlazePose) dan profil waktu. Syarat kedua kaki tetap benar untuk membedakan lompat dari mengangkat satu kaki.
- **Tanpa pose:** lonjakan gerak serentak di seluruh area badan dan kaki, lalu diam. Tidak bisa dibedakan dari jongkok cepat atau menghentak. Mouse: klik.
- **Keandalan:** sedang untuk lompatan ≥10 cm, rendah untuk lompatan kecil.
- **Risiko anak:** tertinggi di antara kandidat (pendaratan, kaus kaki di lantai licin, perabot, laptop tersenggol, lelah). Batasi lompatan beruntun dan beri jeda.

### Jongkok

- **BlazePose:** pinggul turun relatif terhadap lantai (pergelangan kaki), diukur sebagai pecahan panjang kaki saat berdiri. Kaki harus tetap di lantai dan lutut tidak menyentuh lantai. Sudut lutut 2D tidak andal saat anak menghadap kamera, karena paha mengarah ke kamera dan terlihat memendek; jarak vertikal lebih andal.
- **MoveNet:** sama; pinggul, lutut, dan pergelangan kaki ada di 17 titik COCO.
- **Tanpa pose:** frame differencing hanya tahu ada gerak, bukan tinggi badan. Saat anak diam di bawah, tidak ada bedanya dengan diam berdiri. Background subtraction (menyimpan latar kosong) bisa memberi tinggi siluet, tetapi rentan perubahan cahaya dan auto-exposure. Cadangan yang dipakai: sentuh buah di bawah lalu gerak di area kepala (kamera atau mouse), keandalan rendah.
- **Keandalan 320x240:** tinggi. Gerakan lambat (1–2 s per jongkok, 12–24 sampel), perpindahan besar (setengah jongkok ±20 cm ≈ 25 px pada 2 m), dan sendi besar. Risiko teknis: kaki terpotong frame saat laptop di atas meja; detektor punya cadangan skala dari panjang badan.
- **Risiko anak:** rendah (paling buruk jatuh terduduk). Batasi jumlah karena paha lelah, dan jangan wajibkan jongkok dalam.

## c. Rekomendasi: jongkok dulu

1. **Paling andal** di BlazePose dan MoveNet dengan kamera laptop, dan bisa diuji sekarang tanpa memperbaiki pelacakan BlazePose.
2. **Paling aman** untuk anak di antara kandidat.
3. **Paling berguna untuk kombinasi.** Level badan (tinggi–sedang–rendah) adalah dimensi dasar tari dan bela diri:
   - bela diri: kuda-kuda pencak silat, kiba-dachi karate, juchum seogi taekwondo (setengah jongkok yang ditahan), elakan bawah;
   - tari: mendhak di tari Jawa dan Bali, plié balet, gerakan rendah di tari kreasi.
   Detektor memberi nilai kontinu (kedalaman), bukan hanya hitungan, sehingga bisa dibandingkan dengan rekaman ahli (mis. kedalaman kuda-kuda).
4. **Infrastruktur dipakai ulang:** `PoseResult` lengkap, baseline kalibrasi, injeksi pose untuk test, dan overlay debug langsung dipakai detektor lompat (baseline sama) dan detektor memutar.

Urutan berikutnya yang disarankan: hadap samping/balik badan (setelah pelacakan ROI BlazePose diperbaiki; sangat penting untuk struktur jurus/kata yang berganti arah hadap 90°/180°), lalu putar lengan, lompat, dan terakhir putaran penuh 360°.

## d. Konsep permainan jongkok

1. **Pick the Fruit** (diimplementasikan). Buah muncul di tanah di samping kaki. Garis target tampil di layar setinggi pinggul saat setengah jongkok. Turunkan pinggul sampai garis → buah terambil ("Berdiri, masukkan ke keranjang!"). Berdiri tegak → buah masuk keranjang, +1 bintang. Jongkok terlalu dangkal → isyarat ringan "Turun sedikit lagi!". Buah berikutnya muncul setelah jeda, dan ada istirahat setiap 6 buah. Melatih satu siklus jongkok (turun–naik) yang bersih, cocok untuk menguji hitungan detektor.
2. **Hide in the Grass.** Burung hantu sesekali membuka mata. Saat muncul tanda "Sembunyi!", anak jongkok dan menahan posisi 2 detik di balik rumput → bintang. Lalu "Berdiri!". Melatih menahan level rendah (seperti kuda-kuda).
3. **Grow the Flower.** Biji ditanam saat jongkok, bunga tumbuh saat berdiri, mengikuti ketukan tetap (mis. 2 ketuk turun, 2 ketuk naik). Melatih tempo, cocok untuk gerakan tari yang mengikuti musik.
