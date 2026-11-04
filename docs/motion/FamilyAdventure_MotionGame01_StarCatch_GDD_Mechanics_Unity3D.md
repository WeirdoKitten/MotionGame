# Family Adventure — Motion Game 01: STAR CATCH

**Document Type:** GDD + Game Mechanics + Unity 3D Implementation Specification  
**Version:** 1.0  
**Status:** Draft for implementation  
**Platform:** Unity 3D  
**Player:** 1 Child  
**Camera Setup:** Child approximately 1.5 m from camera  
**Session Target:** 2–3 minutes  
**Core Input:** Two hands + body movement + jump

---

# 1. GAME CONCEPT

## 1.1 Game Name
**STAR CATCH**

## 1.2 Game ID
`motion_star_catch`

## 1.3 Core Fantasy
Anak berdiri di depan kamera. Bintang datang dari arah depan menuju area anak. Anak menggerakkan tangan kiri dan kanan untuk menangkap bintang. Bintang tertentu muncul lebih tinggi sehingga anak perlu mengangkat tangan atau melompat.

## 1.4 Core Interaction
`SEE → MOVE → REACH → CATCH → CELEBRATE`

## 1.5 Core Instruction
> "Gerakkan tanganmu dan tangkap bintang!"

Untuk target tinggi:
> "Lompat!"

---

# 2. DESIGN PRINCIPLES
1. Mudah dipahami tanpa tutorial panjang.
2. Dua tangan selalu menjadi bagian dari desain.
3. Anak tetap berada di tempat.
4. Gerakan utama adalah kiri, kanan, atas, dan sesekali melompat.
5. Tidak membutuhkan controller.
6. Tidak ada hukuman keras.
7. Missed star tidak menyebabkan Game Over.
8. Difficulty meningkat bertahap.
9. Calibration wajib sebelum gameplay.
10. Tracking menggunakan koordinat relatif terhadap baseline anak.

---

# 3. PHYSICAL SETUP

## 3.1 Camera Distance
**Reference:** 1.50 meter  
**Recommended setup zone:** 1.35–1.65 meter.

1.5 m adalah reference design distance, bukan pengukuran absolut sampai sentimeter.

## 3.2 Camera Framing
Kamera harus melihat kepala, torso, kedua tangan, dan kedua kaki. Anak tidak perlu berjalan maju/mundur selama gameplay.

## 3.3 Play Area
Gameplay berada pada virtual space yang dinormalisasi terhadap calibration player.

---

# 4. POSITION SYNC & CALIBRATION

Calibration adalah tahap wajib.

```text
CAMERA START
    ↓
DETECT PLAYER
    ↓
FULL BODY DETECTED?
    ↓
CHECK DISTANCE / FRAMING
    ↓
POSITION VALID
    ↓
BODY CALIBRATION
    ↓
HAND CALIBRATION
    ↓
JUMP BASELINE
    ↓
READY
    ↓
STAR CATCH
```

## 4.1 Position Guidance
- Terlalu dekat: **"Mundur sedikit"**
- Terlalu jauh: **"Maju sedikit"**
- Framing tidak lengkap: **"Pastikan seluruh tubuh terlihat"**
- Valid: **"Posisimu sudah pas!"**

## 4.2 Calibration Data
```text
CalibrationData
├── referenceDistance
├── bodyCenter
├── headPosition
├── hipPosition
├── leftHandPosition
├── rightHandPosition
├── leftFootPosition
├── rightFootPosition
├── bodyHeight
└── bodyWidth
```

## 4.3 Relative Coordinates
Gunakan `Player Baseline → Normalize → Gameplay Coordinates`, bukan koordinat kamera mentah. Ini membuat anak dengan tinggi berbeda tetap playable.

---

# 5. MOTION TRACKING

## 5.1 Required Tracking Points
- Head
- Hip / pelvis
- Left hand
- Right hand
- Left foot
- Right foot

## 5.2 Hand Controller
Kedua tangan dapat menangkap object. Tangan yang paling dekat dengan target dapat melakukan capture.

## 5.3 Two-Hand Interaction
Big Star membutuhkan kedua tangan berada di capture zone dalam window yang sama.

---

# 6. NORMALIZED GAMEPLAY SPACE
Horizontal: `LEFT=-1`, `CENTER=0`, `RIGHT=+1`  
Vertical: `LOW=-1`, `MID=0`, `HIGH=+1`.

Gameplay harus tetap relatif terhadap tubuh player.

---

# 7. STAR TYPES

### STAR_NORMAL
- satu tangan cukup
- target kiri/kanan/tengah

### STAR_HIGH
- target area atas
- dapat membutuhkan jump

### STAR_BIG
- ukuran lebih besar
- membutuhkan dua tangan

### STAR_BONUS
- visual celebration khusus
- reward ekonomi tambahan tidak ditetapkan dalam dokumen ini

---

# 8. STAR SPAWN SYSTEM

Bintang muncul dari jauh lalu mendekati player.

```text
FAR
  ⭐
   ↓
    ⭐
     ↓
      ⭐
       ↓
      👦
NEAR
```

Lifecycle:
`SPAWN → APPROACH → CAPTURE_WINDOW → CAUGHT/MISSED → DESPAWN`

Data:
```text
StarSpawnConfig
├── spawnSide
├── targetX
├── targetY
├── approachDuration
├── speed
├── scale
├── starType
└── requiresTwoHands
```

---

# 9. CAPTURE MECHANIC

## 9.1 One-Hand
`distance(hand, star) <= captureRadius` → SUCCESS.

Left atau right hand dapat menangkap.

## 9.2 Two-Hand
```text
distance(leftHand, star) <= radius
AND
distance(rightHand, star) <= radius
```

Kedua tangan harus memenuhi kondisi dalam capture window yang sama.

---

# 10. JUMP MECHANIC

Jump digunakan pada target tertentu agar anak memiliki variasi gerakan.

## 10.1 Detection
Jangan hanya menggunakan posisi tangan. Gunakan perubahan relatif tubuh, dengan `Hip Y` sebagai sinyal utama dan Head/Hands/Feet sebagai supporting signals.

Contoh baseline:
`standingHipY`

Konsep threshold:
`currentHipY - standingHipY > jumpThreshold`

Threshold dikalibrasi per player.

## 10.2 Jump State Machine
`STANDING → TAKEOFF → AIRBORNE → PEAK → LANDING → STANDING`

## 10.3 High Target
High star dapat mensyaratkan tangan mencapai target saat airborne atau menggunakan aturan capture yang dipilih pada implementasi final.

---

# 11. GAMEPLAY FLOW

## Phase 1 — Position Setup
Anak berdiri sekitar 1.5 m dari camera dan framing divalidasi.

## Phase 2 — Calibration
Record standing pose, hand positions/range, dan jump baseline jika jump aktif.

## Phase 3 — Ready
`3 → 2 → 1 → START`

## Phase 4 — Star Catch
Urutan meningkat dari mudah ke kombinasi: center → left/right → mixed → high → two-hand → jump.

## Phase 5 — Celebration
Successful catch memicu star burst, particle, sound, dan feedback singkat.

## Phase 6 — Finish
Tampilkan hasil ringan, bukan ranking kemampuan fisik.

---

# 12. DIFFICULTY PROGRESSION

1. **CENTER:** speed rendah.
2. **LEFT / RIGHT:** gerak horizontal.
3. **MIXED:** left/center/right.
4. **HEIGHT:** low/mid/high.
5. **TWO HANDS:** big star.
6. **JUMP:** high star + jump.
7. **MIX:** semua pola.

Difficulty sebaiknya meningkatkan kompleksitas secara gradual, bukan hanya speed.

---

# 13. MISS / FAILURE
Jika star terlewat, tidak ada Game Over dan tidak ada penalti besar.

Feedback ringan:
> "Coba lagi!"

Star berikutnya tetap muncul.

---

# 14. SESSION DESIGN
Target total: **2–3 menit**.

```text
SETUP       10–20 sec
CALIBRATE   10–20 sec
PLAY        90–120 sec
RESULT      5–10 sec
```

---

# 15. UNITY 3D ARCHITECTURE

```text
MotionStarCatch
│
├── GameManager
├── MotionCamera
├── PlayerTracking
│   ├── Head
│   ├── Hip
│   ├── LeftHand
│   ├── RightHand
│   ├── LeftFoot
│   └── RightFoot
├── CalibrationManager
├── PositionSync
├── GestureManager
│   ├── HandMovementDetector
│   └── JumpDetector
├── GameplaySpace
├── StarSystem
│   ├── StarSpawner
│   ├── StarPool
│   └── StarController
├── CaptureSystem
├── FeedbackSystem
│   ├── VFX
│   ├── SFX
│   └── UI
└── SessionManager
```

---

# 16. RECOMMENDED UNITY SCRIPTS

```text
MotionStarCatchGameManager.cs
MotionCameraController.cs
PlayerTrackingAdapter.cs
PositionSyncController.cs
CalibrationManager.cs
CalibrationData.cs
NormalizedBodySpace.cs
HandMovementDetector.cs
JumpDetector.cs
GestureManager.cs
StarSpawner.cs
StarController.cs
StarSpawnConfig.cs
StarPool.cs
StarCaptureDetector.cs
TwoHandCaptureDetector.cs
FeedbackController.cs
MotionGameUI.cs
SessionManager.cs
DifficultyManager.cs
```

---

# 17. GAME STATE MACHINE

```text
BOOT
 ↓
CAMERA_CHECK
 ↓
PLAYER_DETECT
 ↓
POSITION_SETUP
 ↓
CALIBRATION
 ↓
READY
 ↓
PLAYING
 ↓
RESULT
 ↓
COMPLETE
```

Gameplay internal:
`IDLE → STAR_SPAWN → STAR_APPROACH → CAPTURE_WINDOW → CAUGHT/MISSED → FEEDBACK → NEXT_STAR`

---

# 18. DATA MODEL

```json
{
  "gameId": "motion_star_catch",
  "targetDistance": 1.5,
  "minDistance": 1.35,
  "maxDistance": 1.65,
  "handsRequired": 2,
  "jumpEnabled": true,
  "sessionDuration": 120,
  "difficulty": 1
}
```

Star:
```json
{
  "starType": "NORMAL",
  "targetX": -0.65,
  "targetY": 0.0,
  "speed": 0.8,
  "requiresTwoHands": false
}
```

High star:
```json
{
  "starType": "HIGH",
  "targetX": 0.2,
  "targetY": 0.8,
  "speed": 0.7,
  "requiresJump": true
}
```

Big star:
```json
{
  "starType": "BIG",
  "targetX": 0.0,
  "targetY": 0.2,
  "speed": 0.55,
  "requiresTwoHands": true
}
```

---

# 19. ASSET DIRECTION

Visual style:
- colorful
- friendly
- soft
- child-safe
- clean
- readable
- magical/playful
- tidak menyeramkan

Environment sebaiknya simple magical sky / playful space environment agar tracking player tetap jelas.

---

# 20. 3D ASSETS

## 20.1 Star
Variations:
- normal
- big
- bonus
- high

Requirements:
- bright silhouette
- readable dari jauh
- emissive material
- soft glow
- simple geometry

Prefab:
`Star_01.prefab`

## 20.2 Optional Target Ring
A subtle visual ring can show the capture area without obscuring the child.

---

# 21. STAR VFX

```text
StarSpawnVFX
StarTrailVFX
StarCatchBurstVFX
StarMissVFX
StarGlowVFX
```

Catch:
`STAR → GLOW → BURST → PARTICLES → DESPAWN`

Target duration: 0.3–0.6 sec.

---

# 22. ENVIRONMENT ASSETS

Required:
```text
Sky_Background
Cloud_01
Cloud_02
Cloud_03
Floating_Star_Deco
Soft_Glow
Ground_Shadow
```

Optional:
```text
Moon
Small_Planet
Constellation
Floating_Island
```

Environment must not interfere with body tracking or star readability.

---

# 23. PLAYER VISUALIZATION

The actual child does not need a rendered 3D character. Recommended approach:
- real child remains visible through camera
- virtual stars render around the gameplay space
- optional subtle hand target indicators
- optional body-space guide during setup

Avoid a virtual avatar covering the real child.

---

# 24. CALIBRATION UI ASSETS

```text
UI_Camera_Frame
UI_Player_Outline
UI_Position_OK
UI_Move_Back
UI_Move_Forward
UI_Show_Full_Body
UI_Hands_Ready
UI_Jump_Ready
UI_Countdown
UI_Ready
```

UI setup assets should contain no companion unless explicitly required.

---

# 25. GAMEPLAY UI

Minimal UI:
```text
Top Status
Star Count
Ready Indicator
Instruction Text
Pause
Exit
Result Screen
```

Tidak perlu health bar, minimap, leaderboard, stamina, atau complex HUD.

---

# 26. AUDIO ASSETS

## Music
`BGM_MotionStarCatch_Loop`

Playful, energetic, non-aggressive, child-friendly.

## SFX
```text
SFX_StarSpawn
SFX_StarApproach
SFX_StarCatch
SFX_BigStarCatch
SFX_Jump
SFX_Great
SFX_MissSoft
SFX_Complete
```

---

# 27. FEEDBACK

Successful catch:
- star scale up
- glow
- burst
- particles
- sound
- short positive voice/UI feedback

Examples:
> "Yeay!"
> "Dapat!"
> "Hebat!"

Feedback tidak boleh menghalangi star berikutnya.

---

# 28. SAFETY

## SAFE
- hand movement
- reaching
- standing
- controlled jump

## SUPERVISED
- repeated jumping
- limited physical space
- slippery floor
- nearby furniture

Parent harus memastikan lantai clear, tidak ada benda tajam, ruang cukup, dan footing stabil.

Game tidak membutuhkan running.

---

# 29. TRACKING FAILURE HANDLING

Jika tracking hilang:
```text
TRACKING_LOST
    ↓
PAUSE STAR SPAWN
    ↓
"Pastikan tubuhmu terlihat"
    ↓
TRACKING_RECOVERED
    ↓
RESUME
```

Jika tangan hilang:
> "Pastikan kedua tangan terlihat."

Tidak ada penalti.

---

# 30. ACCEPTANCE CRITERIA

## Position
- [ ] Player detected.
- [ ] Setup targets approximately 1.5 m.
- [ ] 1.35–1.65 m treated as acceptable setup range where depth tracking supports it.
- [ ] Full body visible.
- [ ] Position calibration completes.

## Hands
- [ ] Left hand tracked.
- [ ] Right hand tracked.
- [ ] Both hands independently capture stars.
- [ ] Two-hand star requires both hands.

## Jump
- [ ] Jump baseline calibrated.
- [ ] Jump detection uses relative body movement.
- [ ] High stars can require jump.
- [ ] False triggers minimized.

## Star
- [ ] Star approaches player.
- [ ] Capture window works.
- [ ] Successful capture generates feedback.
- [ ] Missed star does not cause Game Over.
- [ ] Star pool is used.

## Gameplay
- [ ] Session approximately 2–3 minutes.
- [ ] Difficulty increases gradually.
- [ ] Player does not need to walk.
- [ ] No controller required.
- [ ] No leaderboard.
- [ ] No punitive scoring.

## Safety
- [ ] No running required.
- [ ] Jump frequency controlled.
- [ ] Tracking loss pauses progression safely.
- [ ] Parent can exit anytime.

---

# 31. PERFORMANCE REQUIREMENTS

- Use object pooling for stars and VFX.
- Avoid Instantiate/Destroy per star.
- Keep environment lightweight.
- Use simple colliders where needed.
- Prefer deterministic movement/interpolation for stars over unnecessary physics simulation.
- Keep tracking adapter isolated from gameplay logic.

Architecture:
```text
Tracking SDK
     ↓
PlayerTrackingAdapter
     ↓
NormalizedBodySpace
     ↓
GestureManager
     ↓
CaptureSystem
     ↓
GameManager
```

---

# 32. TRACKING SDK ABSTRACTION

Gameplay tidak boleh langsung bergantung pada satu tracking SDK.

Interface concept:
```text
IBodyTrackingProvider

GetHead()
GetHip()
GetLeftHand()
GetRightHand()
GetLeftFoot()
GetRightFoot()
IsTrackingValid()
GetEstimatedDistance()
```

Pipeline:
```text
Camera / Tracking SDK
        ↓
IBodyTrackingProvider
        ↓
Family Adventure Motion Layer
        ↓
STAR CATCH
```

---

# 33. FUTURE REUSABILITY

STAR CATCH menjadi Motion Game pertama dari reusable Motion Game Framework.

Reusable systems:
```text
PositionSync
Calibration
BodyTracking
NormalizedBodySpace
HandTracking
JumpDetection
GestureManager
CaptureSystem
DifficultySystem
FeedbackSystem
```

Framework dapat digunakan untuk game berikutnya seperti catch, swipe, reach, dodge, touch, dan jump.

---

# 34. IMPLEMENTATION ORDER IN UNITY

## Phase 1 — Technical Foundation
1. Create Unity scene.
2. Create MotionCamera.
3. Integrate body tracking provider.
4. Display tracking landmarks.
5. Verify full-body tracking.
6. Verify 1.5 m setup.

## Phase 2 — Position Sync
1. Detect player.
2. Estimate distance if supported.
3. Validate framing.
4. Show move-forward/back instructions.
5. Lock calibration.

## Phase 3 — Calibration
1. Record standing pose.
2. Record hand positions/range.
3. Normalize body space.
4. Calibrate jump baseline.

## Phase 4 — Star Prototype
1. Create Star prefab.
2. Create spawn point.
3. Move star toward player.
4. Add capture radius.
5. Detect hand capture.

## Phase 5 — Two Hands
1. Left-hand capture.
2. Right-hand capture.
3. Two-hand target.
4. Simultaneous detection.

## Phase 6 — Jump
1. High target.
2. Jump detector.
3. Jump capture rule.
4. False-positive testing.

## Phase 7 — Gameplay
1. Star sequence.
2. Difficulty progression.
3. Feedback.
4. Audio.
5. Result screen.

## Phase 8 — Polish
1. VFX.
2. Environment.
3. UI.
4. Audio mix.
5. Performance.
6. Safety testing.

---

# 35. MVP DEFINITION

```text
CAMERA
  ↓
PLAYER DETECTION
  ↓
1.5M POSITION SETUP
  ↓
CALIBRATION
  ↓
LEFT/RIGHT HAND TRACKING
  ↓
STAR APPROACH
  ↓
ONE-HAND CATCH
  ↓
TWO-HAND CATCH
  ↓
HIGH STAR
  ↓
JUMP
  ↓
FEEDBACK
  ↓
RESULT
```

Tidak termasuk MVP:
- complex world
- avatar system
- multiplayer
- leaderboard
- complicated score economy

---

# 36. DEFINITION OF DONE

STAR CATCH siap internal playtest ketika:
1. Anak dapat setup sekitar 1.5 m tanpa bantuan developer.
2. Full body terdeteksi.
3. Kedua tangan dapat digunakan.
4. Star datang menuju player.
5. One-hand catch bekerja.
6. Big star two-hand catch bekerja.
7. High star dapat memicu jump interaction.
8. Miss tidak memberi punishment keras.
9. Tracking loss tidak merusak session.
10. Gameplay berjalan stabil 2–3 menit.
11. Tracking tidak terikat langsung ke gameplay.
12. Core assets dapat diganti melalui prefab/config tanpa mengubah core game logic.

---

# 37. OPEN IMPLEMENTATION NOTES

- Tracking SDK spesifik belum dikunci dalam dokumen ini.
- Metode estimasi jarak 1.5 m bergantung pada hardware/camera/tracking provider yang dipilih.
- Jump threshold final harus ditentukan melalui device testing.
- Visual style asset final perlu mengikuti asset pipeline Family Adventure yang sudah ada.
- Reward/economy dari STAR CATCH belum ditetapkan; jangan menganggap star count otomatis menjadi Love Point.
- Jangan menghubungkan hasil motion game langsung ke Family Mission validation tanpa aturan product terpisah.
