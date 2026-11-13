# 52 — Konversi video senam ke permainan (Ikuti Senam)

Tujuan: gerakan di permainan diambil dari gerakan senam yang nyata, bukan target acak (lihat [docs/51](51%20LATAR%20BELAKANG%20TA.md)). Video senam diubah menjadi urutan gerakan dasar, lalu permainan **Ikuti Senam** memutarnya dengan tempo video. Anak harus menaruh tangan, kaki, dan kepala di posisi yang sama dengan instruktur, pada waktu yang sama.

## Alur

```text
Senam.mp4 ──Video-To-Pose.py──> Builds/Senam/Senam-pose.csv   (BlazePose, 33 titik per frame)
          ──Pose-To-Senam.py──> Resources/Motion/Senam/senam-pemanasan.txt   (koreografi)
                               Builds/Senam/Senam-report.md   (ringkasan)
          ──Senam-Check.py───> Builds/Senam/Senam-targets.jpg, Senam-overlay.mp4   (cek visual)
permainan Ikuti Senam (SenamGame) membaca file koreografi
```

Menjalankan (Python 3, paket di `Tools/requirements.txt`):

```powershell
python -m pip install -r Tools/requirements.txt
python Tools/Video-To-Pose.py Senam.mp4
python Tools/Pose-To-Senam.py Builds/Senam/Senam-pose.csv --name "Senam Pemanasan" --video Senam.mp4
```

Ekstraksi pose memakai model ONNX yang sama dengan permainan (`Resources/Motion/Pose`), dengan pelacakan antar frame seperti `PoseTracker.cs`. Video 4K 126 detik selesai sekitar 3 menit di laptop; pose ditemukan di 3786 dari 3786 frame.

## Koordinat: kenapa tidak dibalik

Anak meniru instruktur seperti bercermin, dan permainan menampilkan kamera anak secara cermin. Jadi target diletakkan di tempat tangan instruktur terlihat di layar, tanpa dibalik. Tangan instruktur yang terlihat di kanan layar menjadi target untuk tangan anak yang juga terlihat di kanan layar. Di file koreografi, "SL"/"SR" berarti terlihat di kiri/kanan layar.

Semua titik relatif terhadap pinggul instruktur saat berdiri (median 6 detik yang bergerak lambat) dan diukur dalam panjang badan (pinggul ke bahu). Karena acuannya lambat, langkah ke samping dan lunge tetap menggeser target, sehingga anak juga harus melangkah. Di permainan titik-titik ini dipetakan ke badan anak yang diukur saat kalibrasi: posisi pinggul, panjang badan, dan panjang kaki (`SenamBody`). Target kaki memakai panjang kaki anak, target tangan dan kepala memakai panjang badan anak.

## Gerakan dasar yang dikenali

| Gerakan dasar | Aturan di konverter | Di permainan | Senam Pemanasan |
|---|---|---|---|
| Menjangkau dua tangan | Kecepatan pergelangan minimum, salah satu tangan ≥ 0,8 badan dari posisi diam | Dua gambar tangan, arah jari mengikuti lengan bawah | 89 |
| Angkat lutut | Satu pergelangan kaki ≥ 0,35 badan lebih tinggi dari kaki lain | Gambar telapak kaki di posisi kaki terangkat | 18 |
| Langkah / kangkang | Pergelangan kaki ≥ 0,45 badan ke samping dari posisi diam | Gambar telapak kaki di posisi langkah | 24 |
| Gerak kepala (baru) | Tekuk ≥ 7° atau tengok ≥ 0,4 (hidung terhadap tengah telinga) | Gambar wajah, dimiringkan atau menoleh | 36 |
| Tangan di pinggang (baru) | Pergelangan di pinggang, siku keluar ≥ 0,6 badan | Dua gambar tangan di pinggang; siku anak harus keluar | 1 (22–37 s) |
| Jalan di tempat (baru) | Pergelangan kaki bergantian naik turun ≥ 2 kali/detik, pinggul tidak bergeser | Dua telapak kaki menyala mengikuti irama, langkah anak dihitung | 4 segmen, 171 langkah |
| Geser badan | Pinggul bergeser ≥ 0,4 badan | Label saja; sudah terwakili posisi target tangan dan kaki | 15 |

Waktu yang tercakup minimal satu gerakan dasar: 97% dari 126 detik.

## Permainan Ikuti Senam

- Kartu ke-4 di menu misi. Kalibrasi sama seperti permainan lain; selama anak diam, badan anak diukur dari pose.
- Figur pemandu (instruktur) di kanan atas, dan bayangan transparan instruktur di atas badan anak.
- Gambar target muncul 0,8 detik sebelum waktunya dan bisa diraih dari 0,35 detik sebelum sampai 0,45 detik sesudah pose (lebih lama untuk pose yang ditahan).
- Tertangkap bila pergelangan tangan/kaki anak berada dalam **0,35 panjang badan** dari target (`SenamJudge.Reach`). Tangan mana pun boleh, tetapi gambar tangan selalu berpasangan sehingga kedua tangan harus ikut. Kepala harus miring/menoleh ke arah yang sama minimal setengah dari instruktur.
- Satu gerakan lengkap (misalnya kedua tangan) = satu bintang; jalan di tempat = satu bintang tiap 4 langkah. Meleset tidak mengurangi apa pun.
- Tempo mengikuti video (`SenamGame.Speed = 1`); bisa diperlambat untuk anak kecil.
- Log penelitian per target (tertangkap, keterlambatan, jarak terdekat) disimpan ke `Application.persistentDataPath/senam/*.csv` dan tidak ditampilkan ke anak.
- Tanpa model pose, target bereaksi ke gerak kamera seperti permainan lain (bisa dimainkan, tetapi bukan tiruan senam yang sebenarnya).

## Verifikasi

1. **Visual.** `Senam-targets.jpg` berisi 156 momen target yang digambar kembali di atas video asli; lingkaran kuning (tangan), biru (kaki), ungu (kepala), dan oranye (pinggang) jatuh di bagian badan instruktur yang benar. `Senam-overlay.mp4` memutar seluruh video dengan target muncul seperti di permainan.
2. **Penilai (`SenamJudge`) dengan anak tiruan.** Pose instruktur dipasang ke badan anak yang lebih kecil dan berdiri sedikit ke kiri (`SenamPlayTests`, dan harness .NET di luar Unity):

| Anak tiruan | Gerakan lengkap | Langkah jalan di tempat |
|---|---|---|
| Meniru persis | 168/168 (100%) | 112 |
| Meniru terlambat 0,4–0,5 s, posisi bergetar ±0,02–0,03 | 168/168 (100%) | 120–121 |
| Hanya tangan yang meniru, kaki diam | 127/168 (76%) | 0 |
| Diam saja | 1/168 (0,6%) | 0 |

   Pemilihan radius: 0,30 / 0,35 / 0,40 badan memberi 100% untuk peniru di ketiganya, dan untuk anak diam 0,6% / 0,6% / 1,2% gerakan (7,9% / 9,6% / 12,4% target tangan tunggal). Dipilih 0,35.

3. **Belum diuji:** permainan di Unity Editor (lisensi Unity perlu login ulang), dan dengan anak sungguhan di depan kamera. Jalankan `Tools\Test-Project.ps1` setelah lisensi aktif; tes `SenamPlayTests` juga menyimpan `Builds/Screenshots/motion-senam.png`.

## Batasan

- Kamera 2D: condong dan putar badan hanya terwakili lewat posisi tangan dan kepala, bukan sudut badan.
- Bentuk tangan (terbuka, kepal) belum dibaca; gambar tangan di Ikuti Senam selalu terbuka.
- Tempo video bisa terlalu cepat untuk anak; perlu dicoba `Speed` 0,75.
- Video diunduh dari internet: video tidak ikut di repository (`*.mp4` di `.gitignore`). Untuk publikasi, sebaiknya rekam senam sendiri dan konversi ulang.
- Angka di atas adalah konsistensi pipeline (instruktur terhadap dirinya sendiri), bukan akurasi pada anak. Akurasi pada anak diukur nanti dari log penelitian.
