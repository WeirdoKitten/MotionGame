# 51 — Latar belakang tugas akhir

Catatan alasan memilih Motion Game sebagai tugas akhir, supaya arah penelitian tidak bergeser.

## Masalah

Anak yang bermain gadget kebanyakan hanya menekan layar dan hampir tidak bergerak. Game gerak (exergame) bisa membuat anak aktif secara fisik saat bermain.

## Kondisi Motion Game sekarang

Motion Game sudah mengubah gerak badan menjadi input permainan lewat kamera. Namun target yang harus dikejar anak tidak berasal dari gerakan nyata:

- **Catch the Star**: bintang muncul di 10 posisi tetap, lalu posisinya diacak sedikit.
- **Follow the Line**: lima bentuk garis buatan (lurus, zigzag, gelombang, busur).
- **Avoid the Rock**: jalur batu dipilih secara acak.

Akibatnya, gerakan anak saat bermain hanyalah gerakan acak, bukan gerakan yang punya bentuk di dunia nyata.

## Tujuan

Gamifikasi gerakan dunia nyata. Gerakan yang dilakukan anak di dalam game harus berasal dari gerakan yang memang ada, seperti senam, tarian, dan bela diri.

## Tahapan

1. **Gerakan dasar.** Satu game melatih satu gerakan dasar (menjangkau, menelusuri, bergeser, jongkok, putar lengan, lompat, putar badan, dan gerakan baru lainnya). Detektor gerakan dibuat terpisah dari tampilan game agar bisa dipakai ulang.
2. **Konversi gerakan nyata ke gerakan dasar.** Ada dua cara:
   - dengan penanda: gelang warna di tangan (mis. hijau) dan kaki (mis. merah);
   - tanpa penanda: video orang bersenam diubah langsung menjadi urutan gerakan dasar.
3. **Kombinasi.** Gerakan dasar dirangkai menjadi rangkaian tetap, misalnya satu senam utuh.
4. **Game mengikuti rangkaian.** Posisi bintang, jalur garis, dan waktu batu diambil dari rangkaian hasil konversi, bukan diacak.

## Urutan lingkup

Mulai dari **senam** karena gerakannya paling sederhana dan berulang. Setelah senam berhasil, lingkup diperluas ke **tarian**, lalu **bela diri**, dengan rangkaian dari rekaman ahli.

## Prinsip yang tetap berlaku

- Tidak ada game over dan tidak ada skor performa untuk anak. Pengukuran akurasi hanya untuk penelitian.
- Kamera hanya aktif selama bermain dan dimulai dengan sengaja oleh pengguna.
- Uji dengan anak hanya dilakukan setelah ada persetujuan orang tua.

## Rencana kerja

Rencana kegiatan 16 November–31 Desember 2026 ada di `D:\TA\Log-TA-Motion-Game.xlsx`, di luar repository ini. Sheet **Kerjaan** berisi daftar tugas per minggu, dan sheet **Harian** berisi catatan pengerjaan per hari. Tugas dikerjakan oleh Satria (S) dan Faruq (F).
