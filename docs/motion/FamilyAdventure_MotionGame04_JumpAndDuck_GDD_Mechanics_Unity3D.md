# Family Adventure — Motion Game 04
# JUMP AND DUCK
## GDD + Mechanics + Unity 3D Implementation Specification

**Document Type:** GDD + Game Mechanics + Computer Vision & Kinematics Specification  
**Version:** 1.0  
**Status:** Implementation Ready (Specification for Agent / Developer)  
**Platform:** Unity 3D (URP 2D / Canvas Overlay)  
**Player:** 1 Child  
**Camera Setup:** Child approximately 1.5 m from front camera  
**Session Target:** 60 seconds per round (Standard Motion Game Session)  
**Primary Input:** Lower Body Kinematics (Vertical Center of Mass + Knee Angle + Ankle Ground Clearance)  
**Core Actions:** Jump (⬆️ Melompat) & Duck / Squat (⬇️ Merunduk)

---

# 1. GAME CONCEPT

## 1.1 Game Name
**JUMP AND DUCK** (Misi Gerak: *Lompat & Merunduk*)

## 1.2 Game ID
`motion_jump_duck`

## 1.3 Core Fantasy
Pemain berdiri di depan kamera seolah sedang berlari di lintasan alam terbuka (sungai/hutan). Rintangan datang secara berirama dari kejauhan menuju ke arah pemain.
- Jika rintangan datang di bagian **bawah** (gelondongan kayu mengapung / arus air / duri): pemain harus **melompat** melewatinya.
- Jika rintangan datang di bagian **atas** (cabang pohon rendah / sarang lebah / batu gantung): pemain harus **merunduk atau jongkok** di bawahnya.

## 1.4 Core Interaction
`SEE OBSTACLE → IDENTIFY HEIGHT → TIME ACTION (JUMP / DUCK) → CLEAR OBSTACLE → CELEBRATE`

## 1.5 Core Instruction
> "Lompat untuk rintangan bawah, merunduk untuk rintangan atas!"

Saat rintangan bawah tiba:
> "Lompat!"

Saat rintangan atas tiba:
> "Merunduk!"

---

# 2. DESIGN PRINCIPLES
1. **Instan Dipahami:** Anak langsung memahami stimulus visual (bawah = lompat, atas = merunduk) tanpa tutorial teks panjang.
2. **Tanpa Kegagalan Kasar (No Hard Failure / No Game Over):** Jika pemain terlambat melompat atau merunduk, rintangan hanya melewatinya dengan isyarat visual/audio lembut (*"Lompat lebih tinggi!"* / *"Awas kepalanya!"*). Skor tidak berkurang.
3. **Invariansi Biometrik:** Anak dengan tinggi badan berbeda dan pada jarak berbeda tetap dapat bermain secara adil berkat normalisasi skala tubuh (*skeletal proportion normalization*).
4. **Respon Cepat Tanpa Lag:** Filter sinyal harus adaptif (*One Euro Filter*) agar koordinat stabil saat diam namun nol latensi saat bergerak cepat.
5. **Dukungan Pengujian (Testable Fallback):** Tetap dapat dimainkan dan diuji secara otomatis melalui PlayMode test atau input simulasi keyboard/mouse.

---

# 3. PHYSICAL SETUP

## 3.1 Camera Distance
- **Reference:** 1.50 meter
- **Recommended Setup Zone:** 1.35 – 1.65 meter
- Bidang pandang harus menangkap seluruh tubuh dari kepala hingga telapak kaki saat berdiri tegak.

## 3.2 Camera Framing & Play Area
```text
                  CAMERA
                    📷
                     |
                     | ~1.5m
                     |
                     ▼
           ┌──────────────────┐
           │      (O)  Head   │
           │     / | \ Arms   │
           │    | (H) | Hips  │ ◄── Baseline Y
           │     /   \ Knees  │
           │    👣   👣 Feet  │
           └──────────────────┘
```

---

# 4. POSITION SYNC & CALIBRATION

Menggunakan alur kalibrasi standar yang sudah ada di `MotionGame`:

```text
CAMERA START
    ↓
STAND INSIDE SILHOUETTE GUIDE
    ↓
PRESENCE DETECTED (Motion in BodyZone)
    ↓
HOLD STILL FOR 5 SECONDS (Green bar fills)
    ↓
CAPTURE BASELINE KINEMATICS:
├── Y_baseline (Initial Hip Center Y)
├── Y_floor (Ankle baseline Y)
├── L_ref (Distance from Hip to Ankle)
└── Knee Rest Angle (~170°-180°)
    ↓
3-2-1 COUNTDOWN
    ↓
GAMEPLAY START (60 Seconds)
```

### Parameter Baseline yang Disimpan saat Kalibrasi:
* `Y_baseline`: Rata-rata posisi vertikal pinggul (*mid-point left & right hip*) saat berdiri diam tegak.
* `Y_floor`: Posisi rata-rata vertikal mata kaki saat menyentuh lantai virtual.
* `L_ref`: Jarak vertikal $\| P_{hip} - P_{ankle} \|$ sebagai unit skala referensi normalisasi tubuh.

---

# 5. COMPUTER VISION & KINEMATICS SPECIFICATION

## 5.1 Required Tracking Landmarks
Landmark diekstrak dari `PoseTracker` (BlazePose 33 titik atau MoveNet 17 titik):
1. **Hips:** Left Hip & Right Hip $\rightarrow$ $P_{hip} = \frac{P_{hip\_L} + P_{hip\_R}}{2}$
2. **Knees:** Left Knee ($P_{knee\_L}$) & Right Knee ($P_{knee\_R}$)
3. **Ankles:** Left Ankle ($P_{ankle\_L}$) & Right Ankle ($P_{ankle\_R}$)

## 5.2 Normalisasi Koordinat Relatif
Untuk menghindari pengaruh tinggi anak dan jarak kamera:
$$\Delta Y_{norm} = \frac{Y_{hip} - Y_{baseline}}{L_{ref}}$$
* Nilai positif ($+$): Pinggul naik di atas garis berdiri (indikasi melompat).
* Nilai negatif ($-$) : Pinggul turun di bawah garis berdiri (indikasi merunduk/jongkok).

## 5.3 Perhitungan Sudut Sendi Lutut ($\theta_{knee}$)
Sudut fleksi lutut dihitung melalui dot product vektor 3 sendi (Pinggul, Lutut, Mata Kaki):
$$\vec{u} = P_{hip} - P_{knee}, \quad \vec{v} = P_{ankle} - P_{knee}$$
$$\theta_{knee} = \arccos\left(\frac{\vec{u} \cdot \vec{v}}{\|\vec{u}\| \|\vec{v}\|}\right)$$
* **Berdiri Normal:** $\theta_{knee} \ge 160^\circ$
* **Fase Merunduk / Squat:** $\theta_{knee} \le 135^\circ$

## 5.4 Penapisan Sinyal (One Euro Filter)
Untuk mengeliminasi getaran landmark (*jitter*) tanpa menambah lag:
* Parameter filter pada koordinat $Y_{hip}$ dan sudut $\theta_{knee}$:
  * $f_{c,min} = 1.0\text{ Hz}$ (cutoff frekuensi saat diam/kecepatan rendah)
  * $\beta = 0.007$ (koefisien sensitivitas kecepatan)
  * $f_{c,cutoff\_deriv} = 1.0\text{ Hz}$
* **Perilaku:** Saat anak berdiri diam, fluktuasi sub-piksel diredam (anti-false jump). Saat anak melompat cepat, cutoff melonjak instan sehingga responsivitas 0 delay.

---

# 6. ACTION RECOGNITION (FINITE STATE MACHINE)

```text
               ┌───────────────────────────┐
               │      STATE: STANDING      │
               └─────┬───────────────┬─────┘
                     │               │
      (ΔY_norm > +0.10)              │ (ΔY_norm < -0.15
      & (Y_ankle > Y_floor + 0.05)   │  & θ_knee < 135°)
                     ▼               ▼
               ┌───────────┐   ┌───────────┐
               │ AIRBORNE  │   │  DUCKING  │
               │  (JUMP)   │   │  (SQUAT)  │
               └─────┬─────┘   └─────┬─────┘
                     │               │
           (ΔY_norm < +0.04)         │ (ΔY_norm > -0.08
                     │               │  & θ_knee > 155°)
                     ▼               ▼
               ┌───────────┐   ┌───────────┐
               │  LANDING  │   │ RECOVERED │
               └─────┬─────┘   └─────┬─────┘
                     └───────┬───────┘
                             ▼
                     [Kembali STANDING]
```

### Aturan Pencegahan Ambiguitas (Pre-jump Dip vs Duck):
Saat anak bersiap melompat, pinggul akan turun sesaat (*dip*) selama $< 0.25$ detik sebelum melesat ke atas. State `Ducking` hanya divalidasi aktif jika pemain menahan posisi jongkok $\ge 0.3$ detik, atau jika tidak terjadi pembalikan kecepatan vertikal yang sangat cepat ke atas.

---

# 7. OBSTACLE TYPES & SPAWNING PATTERNS

## 7.1 Karakteristik Rintangan
| Tipe Rintangan | Posisi Datang | Visual Objek | Gerakan Pemain yang Valid | Target Zone di Layar |
|---|---|---|---|---|
| **LOW_OBSTACLE** | Bawah (Lantai) | Gelondongan kayu / arus air | **JUMP** (Melompat) | $Y \in [0.05, 0.25]$ |
| **HIGH_OBSTACLE** | Atas (Kepala) | Ranting gantung / lebah melayang | **DUCK** (Merunduk) | $Y \in [0.65, 0.85]$ |

## 7.2 Pergerakan Objek (Approach Mechanics)
* Mirip dengan batu pada *Avoid the Rock*:
  * Objek muncul di horizon ($Z$ jauh, skala kecil $0.05$) di posisi tengah atas ($Y \approx 0.65$).
  * Bergerak membesar dan meluncur mendekati anak selama $T_{travel} = 2.4$ detik.
  * Ukuran membesar dari skala $0.05 \rightarrow 0.20$.
  * Rintangan bawah meluncur ke jalur bawah ($Y \rightarrow 0.14$).
  * Rintangan atas meluncur melayang di area kepala ($Y \rightarrow 0.75$).

## 7.3 Spawning Progression
* Total ronde: **60 detik**.
* Interval antar rintangan: Dimulai dari 2.5 detik, berangsur menurun hingga 1.8 detik di akhir ronde.
* Urutan rintangan:
  1. Wave 1–3: Rintangan tunggal acak (Bawah $\rightarrow$ Atas $\rightarrow$ Bawah) untuk pengenalan.
  2. Wave 4+: Pola acak dinamis dengan jeda yang cukup bagi anak untuk mendarat sebelum rintangan berikutnya muncul.

---

# 8. SCORING & FEEDBACK SPECIFICATION

## 8.1 Kondisi Evaluasi saat Rintangan Tiba ($t = T_{travel}$):
1. **Kasus Rintangan Bawah (LOW):**
   * **Sukses:** Status pemain saat benturan adalah `AIRBORNE` (atau $\Delta Y_{norm} > 0.10$).
     * Hasil: $+1$ Skor, partikel emas/bintang meletup (*Burst*), cue *"Hebat!"*.
   * **Gagal/Meleset:** Pemain masih berada di `STANDING` atau `DUCKING`.
     * Hasil: Tidak ada penambahan skor, cue *"Lompat!"*.
2. **Kasus Rintangan Atas (HIGH):**
   * **Sukses:** Status pemain saat benturan adalah `DUCKING` (atau $\Delta Y_{norm} < -0.12$).
     * Hasil: $+1$ Skor, partikel hijau/bintang meletup (*Burst*), cue *"Keren!"*.
   * **Gagal/Meleset:** Pemain berada di `STANDING` atau `AIRBORNE`.
     * Hasil: Tidak ada penambahan skor, cue *"Merunduk!"*.

## 8.2 Feedback Layar Akhir
* Menggunakan panel standar `Finish()` dari `MotionGame`:
  * Banner *"Hebat!"*
  * Bintang skor (`Result star`) dan jumlah skor $\times N$.
  * Tombol **Main lagi** (*Restart*) dan **Misi lain** (*Back to menu*).

---

# 9. ASSET & UI SPECIFICATION

## 9.1 Asset References (Resources/Motion/)
* **Background Texture:** `bg-jump.png` (tema jalur alam/sungai dengan horizon terbuka).
* **Sprite Rintangan Bawah:** `log.png` atau `puddle.png` (gelondongan kayu atau rintangan lantai).
* **Sprite Rintangan Atas:** `branch.png` atau `bee.png` (ranting kayu gantung atau rintangan melayang).
* **Indicator Pads:**
  * Indikator lantai (berkedip merah saat rintangan bawah mendekat).
  * Indikator langit/atas (berkedip kuning/merah saat rintangan atas mendekat).

## 9.2 Fallback Input Controls (Testing Mode)
Untuk keperluan uji coba tanpa webcam atau automated testing:
* **Tombol Panah Atas / Spasi:** Mensimulasikan kondisi `JUMP` (mengangkat $Y_{hip}$ selama 0.4 detik).
* **Tombol Panah Bawah / Ctrl:** Mensimulasikan kondisi `DUCK` (menurunkan $Y_{hip}$ selama ditekan).
* **Klik Layar:** Klik bagian atas area layar = Duck, klik bagian bawah = Jump.

---

# 10. ARCHITECTURE & CODE INTEGRATION

### File yang Dibuat / Dimodifikasi:
1. `Assets/_Project/Scripts/MotionGame/JumpDuckGame.cs` *(File Baru)*:
   * Mengimplementasikan `sealed class JumpDuckGame : MotionGame`.
   * Berisi loop rintangan, logika kalkulasi kinematics ($P_{hip}, P_{knee}, P_{ankle}$), dan evaluasi sukses/gagal.
2. `Assets/_Project/Scripts/MotionGame/OneEuroFilter.cs` *(File Baru / Helper)*:
   * Menyediakan kelas filter 1D independen untuk smoothing sinyal realtime.
3. `Assets/_Project/Scripts/MotionGame/MotionGames.cs` *(Modifikasi Menu)*:
   * Menambahkan entri ke-4 pada `MotionMissionMenu.Titles` (`"Jump & Duck"`).
   * Menambahkan `bg-jump` pada `Backgrounds`.
   * Menghubungkan indeks ke-3 untuk instansiasi `JumpDuckGame`.
4. `Assets/_Project/Tests/PlayMode/MotionGamesPlayTests.cs` *(Uji Otomatis)*:
   * Menambahkan unit test `JumpDuckGame_RespondsToJumpAndDuck()`.

