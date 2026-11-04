# Family Adventure — Motion Game 03
# AVOID THE ROCK
## Mechanics + Assets Specification for Unity 3D

**Version:** 1.0  
**Status:** Implementation Draft  
**Game Type:** Full-Body Motion / Physical Activity / Avoidance  
**Player:** 1 Child  
**Reference Camera Distance:** ~1.5 m  
**Primary Movement:** Body Left / Right + Jump + Duck  
**Session Target:** 2–3 minutes

---

# 1. GAME CONCEPT

**AVOID THE ROCK** adalah motion game olahraga ringan untuk anak.

Anak berdiri sekitar 1,5 meter dari kamera. Batu datang dari arah depan menuju area player. Anak harus membaca arah dan posisi batu lalu menghindarinya menggunakan gerakan tubuh.

Gerakan utama:

- ↔️ Geser/lean badan ke kiri
- ↔️ Geser/lean badan ke kanan
- ⬆️ Melompat
- ⬇️ Menunduk

Fokus game:

> **Reaction + Coordination + Body Control + Agility**

Core fantasy:

> **"Hindari batu yang datang!"**

---

# 2. CORE GAMEPLAY

```text
SEE ROCK
   ↓
IDENTIFY TRAJECTORY
   ↓
MOVE BODY
   ↓
AVOID
   ↓
SUCCESS
   ↓
NEXT ROCK
```

Gameplay tidak mengharuskan anak berlari.

Anak tetap berada pada physical play area yang sama.

---

# 3. PHYSICAL SETUP

## 3.1 Camera Distance

Reference:

**1.50 meter**

Recommended setup zone:

**1.35–1.65 meter**

1.5 m adalah reference design distance.

## 3.2 Player Position

```text
             CAMERA
                📷
                 |
                 | ~1.5m
                 |
                 ▼

          ┌─────────────┐
          │      👦     │
          │     /|\     │
          │     / \     │
          │    👣 👣    │
          └─────────────┘
```

## 3.3 Full Body Visibility

Minimum tracking:

```text
Head
Shoulders
Hip
Left Knee
Right Knee
Left Foot
Right Foot
```

Hands tetap dapat dilacak oleh Motion Framework, tetapi Game 03 tidak menjadikan tangan sebagai primary avoidance controller.

---

# 4. POSITION SYNC

Position Sync dilakukan sebelum gameplay.

```text
PLAYER DETECT
     ↓
FULL BODY CHECK
     ↓
POSITION CHECK
     ↓
VALID?
 ┌───┴────┐
 NO       YES
 ↓         ↓
GUIDE     LOCK
PLAYER    POSITION
           ↓
       CALIBRATION
```

Too close:

> "Mundur sedikit."

Too far:

> "Maju sedikit."

Body tidak lengkap:

> "Pastikan seluruh tubuh terlihat."

Valid:

> "Bagus! Posisimu sudah pas."

---

# 5. CALIBRATION

Calibration harus dilakukan sebelum game.

## 5.1 Standing Baseline

Record:

```text
headPosition
shoulderPosition
hipPosition
leftKneePosition
rightKneePosition
leftFootPosition
rightFootPosition
bodyHeight
bodyWidth
```

## 5.2 Side Movement Baseline

Instruksi:

> "Gerak ke kiri sedikit."

Record:

`leftBodyRange`

Instruksi:

> "Sekarang ke kanan."

Record:

`rightBodyRange`

Gerakan yang diinginkan adalah controlled side shift/lean, bukan lari.

## 5.3 Duck Calibration

Instruksi:

> "Coba menunduk."

Record:

`duckThreshold`

## 5.4 Jump Calibration

Instruksi:

> "Lompat kecil."

Record:

`jumpThreshold`

Threshold dibuat relatif terhadap baseline dan ukuran tubuh anak.

---

# 6. NORMALIZED BODY SPACE

Gunakan normalized body space.

Horizontal:

```text
LEFT         CENTER         RIGHT
 -1            0              +1
```

Vertical:

```text
HIGH         +1
MID           0
LOW          -1
```

Recommended avoidance lanes:

```text
+--------------------------------+
|                                |
| LEFT | CENTER | RIGHT          |
|      |        |                |
|      |   👦   |                |
|      |        |                |
+--------------------------------+
```

Untuk implementasi yang lebih halus, lane bukan perpindahan teleport.

Player position dapat berupa continuous normalized X.

---

# 7. AVOIDANCE ZONES

Game menggunakan tiga horizontal zones utama:

```text
LEFT       CENTER       RIGHT
```

Rock trajectory menentukan zona yang harus dihindari.

Contoh:

```text
ROCK → LEFT

       🪨
        ↓
       LEFT

PLAYER
        👦
```

Player harus bergerak ke center/right.

---

# 8. ROCK TYPES

## 8.1 ROCK_LEFT

Batu datang melalui kiri.

Required response:

`MOVE RIGHT`

## 8.2 ROCK_RIGHT

Batu datang melalui kanan.

Required response:

`MOVE LEFT`

## 8.3 ROCK_HIGH

Batu datang melalui area atas.

Required response:

`DUCK`

## 8.4 ROCK_LOW

Batu datang melalui area bawah.

Required response:

`JUMP`

## 8.5 ROCK_CENTER

Batu datang langsung menuju center.

Response:

- left
- right
- atau jump/duck sesuai trajectory yang ditentukan.

Game sebaiknya tidak memberikan terlalu banyak pilihan pada level awal.

---

# 9. ROCK APPROACH

Batu harus terlihat datang dari jauh.

```text
FAR

        🪨

         ↓

          🪨

           ↓

            🪨

             ↓

             👦

NEAR
```

Rock memiliki:

```text
spawnPosition
targetPosition
speed
trajectory
size
height
lane
```

---

# 10. ROCK LIFECYCLE

```text
SPAWN
 ↓
APPROACH
 ↓
WARNING
 ↓
AVOIDANCE WINDOW
 ↓
AVOIDED / HIT
 ↓
FEEDBACK
 ↓
DESPAWN
```

---

# 11. SIDE DODGE MECHANIC

## 11.1 Move Right

Rock datang dari left.

```text
🪨 → → →

LEFT          CENTER        RIGHT

              👦
```

Player melakukan side movement ke kanan.

## 11.2 Move Left

Rock datang dari right.

```text
← ← ← 🪨

LEFT          CENTER        RIGHT

 👦
```

Player melakukan side movement ke kiri.

---

# 12. SIDE MOVEMENT DETECTION

Gunakan hip + torso movement.

Primary:

`Hip normalized X`

Supporting:

- shoulder center X
- torso center X
- foot positions

Concept:

```text
baselineHipX
      ↓
currentHipX
      ↓
deltaX
      ↓
SIDE SHIFT
```

Example:

```text
deltaX > rightThreshold
→ MOVED_RIGHT

deltaX < leftThreshold
→ MOVED_LEFT
```

Smoothing wajib digunakan untuk mengurangi noise tracking.

---

# 13. DUCK MECHANIC

Duck digunakan untuk batu yang melewati bagian atas tubuh.

Concept:

```text
      🪨
      ↓
────────────
      👦
      ↓
    DUCK
```

Detect relative head/shoulder movement.

Primary:

`HeadY`

Supporting:

`HipY`

Concept:

```text
baselineHeadY
      ↓
currentHeadY
      ↓
deltaY < duckThreshold
      ↓
DUCK
```

Jangan menggunakan absolute screen coordinate.

---

# 14. JUMP MECHANIC

Low rock dapat dihindari dengan melompat.

```text
🪨 ─────────→

       👦
       ↑
      JUMP
```

Jump menggunakan body movement.

Primary:

`HipY`

Supporting:

- head Y
- knees
- feet

State:

```text
STANDING
 ↓
TAKEOFF
 ↓
AIRBORNE
 ↓
PEAK
 ↓
LANDING
 ↓
STANDING
```

---

# 15. AVOIDANCE VALIDATION

Avoidance bukan collision physics biasa.

System mengevaluasi:

```text
rockTrajectory
+
playerBodyPosition
+
requiredAvoidDirection
+
timingWindow
```

Contoh:

```text
Rock trajectory = LEFT

Player:
baseline X = 0
current X = +0.45

→ SAFE
```

Jika player tetap center:

```text
Rock trajectory = LEFT
Player X = 0
Avoidance = FALSE
```

---

# 16. SAFE CORRIDOR

Player tidak perlu berpindah jauh.

```text
LEFT SAFE
    ↓
┌───────────────┐
│               │
│    PLAYER     │
│       👦      │
│               │
└───────────────┘
```

Recommended side movement harus kecil dan nyaman.

Game tidak boleh memaksa anak melakukan extreme lateral movement.

---

# 17. COLLISION WINDOW

Rock memiliki danger window.

```text
FAR
  ↓
APPROACH
  ↓
WARNING
  ↓
DANGER WINDOW
  ↓
PASS
```

Player harus sudah berada di safe zone sebelum rock masuk danger window.

---

# 18. HIT HANDLING

Jika player gagal:

```text
ROCK
 ↓
PLAYER
 ↓
SOFT HIT
```

Feedback:

- soft impact VFX
- short sound
- visual reset

Tidak ada:

- HP
- Game Over
- punishment keras
- loud negative sound

Next rock tetap dapat muncul.

---

# 19. GAMEPLAY FLOW

## Phase 1 — Setup

Player berdiri sekitar 1.5 m.

## Phase 2 — Calibration

Calibrate:

- center
- left shift
- right shift
- duck
- jump

## Phase 3 — Ready

```text
3
2
1
GO!
```

## Phase 4 — Avoid

Rock mulai muncul.

Awal:

```text
LEFT
RIGHT
```

Kemudian:

```text
DUCK
JUMP
```

Kemudian kombinasi.

## Phase 5 — Result

```text
✨ GREAT JOB! ✨

Batu berhasil dihindari:
XX

"Hebat!"
```

---

# 20. DIFFICULTY PROGRESSION

## LEVEL 1

Only:

- left rock
- right rock

Slow speed.

## LEVEL 2

Add:

- center rock
- larger reaction variety

## LEVEL 3

Add:

- low rock
- jump

## LEVEL 4

Add:

- high rock
- duck

## LEVEL 5

Mixed:

```text
LEFT
RIGHT
JUMP
DUCK
LEFT
RIGHT
```

## LEVEL 6

Faster controlled sequence.

Do not make speed increase unlimited.

Difficulty can also increase through trajectory complexity.

---

# 21. PATTERN SEQUENCES

Example:

```text
🪨 LEFT
   ↓
MOVE RIGHT

🪨 RIGHT
   ↓
MOVE LEFT

🪨 LOW
   ↓
JUMP

🪨 HIGH
   ↓
DUCK
```

Advanced:

```text
LEFT
RIGHT
JUMP
LEFT
DUCK
RIGHT
JUMP
```

Avoid impossible back-to-back patterns.

Example to avoid:

```text
JUMP
immediately
JUMP
immediately
JUMP
```

This can create unnecessary physical fatigue.

---

# 22. UNITY 3D ARCHITECTURE

```text
MotionAvoidRock
│
├── GameManager
├── MotionCamera
│
├── Tracking
│   ├── BodyTrackingProvider
│   ├── PlayerTrackingAdapter
│   └── TrackingDebug
│
├── PositionSync
│   └── PositionSyncController
│
├── Calibration
│   ├── CalibrationManager
│   └── CalibrationData
│
├── Motion
│   ├── NormalizedBodySpace
│   ├── SideShiftDetector
│   ├── DuckDetector
│   ├── JumpDetector
│   └── GestureManager
│
├── ObstacleSystem
│   ├── RockSpawner
│   ├── RockPool
│   ├── RockController
│   ├── RockTrajectory
│   └── AvoidanceValidator
│
├── Feedback
│   ├── VFXController
│   └── SFXController
│
└── UI
    ├── SetupUI
    ├── GameplayUI
    └── ResultUI
```

---

# 23. UNITY SCRIPTS

Recommended:

```text
MotionAvoidRockGameManager.cs
MotionCameraController.cs

BodyTrackingProvider.cs
PlayerTrackingAdapter.cs

PositionSyncController.cs

CalibrationManager.cs
CalibrationData.cs

NormalizedBodySpace.cs
SideShiftDetector.cs
DuckDetector.cs
JumpDetector.cs
GestureManager.cs

RockSpawner.cs
RockPool.cs
RockController.cs
RockTrajectory.cs
RockSpawnConfig.cs
AvoidanceValidator.cs
DangerWindowController.cs

DifficultyManager.cs

FeedbackController.cs
VFXController.cs
SFXController.cs

SetupUIController.cs
GameplayUIController.cs
ResultUIController.cs

SessionManager.cs
```

---

# 24. TRACKING ABSTRACTION

Use:

`IBodyTrackingProvider`

Required:

```text
IsTrackingValid()

GetHead()
GetShoulders()
GetHip()

GetLeftKnee()
GetRightKnee()

GetLeftFoot()
GetRightFoot()

GetEstimatedDistance()
```

Pipeline:

```text
Tracking SDK
      ↓
IBodyTrackingProvider
      ↓
PlayerTrackingAdapter
      ↓
NormalizedBodySpace
      ↓
GestureManager
      ↓
Avoidance System
```

---

# 25. ROCK PREFAB

Primary:

`PF_Rock_Normal`

Hierarchy:

```text
PF_Rock_Normal
├── Mesh
├── Shadow
├── WarningGlow
├── DangerCollider
├── Trail
└── AudioSource
```

Variants:

```text
PF_Rock_Small
PF_Rock_Normal
PF_Rock_Large
PF_Rock_High
PF_Rock_Low
```

---

# 26. ROCK VISUAL STYLE

Style:

- soft cartoon
- colorful
- rounded
- friendly
- not realistic
- not scary

Avoid sharp jagged geometry.

The rock should visually communicate trajectory clearly.

Optional:

- glowing outline
- small trail
- subtle bounce/rotation

---

# 27. ROCK ASSETS

Required:

```text
ROCK_01
ROCK_02
ROCK_03
ROCK_04
ROCK_05
```

Variations can differ in:

- shape
- size
- subtle surface pattern

Do not make rocks visually threatening.

---

# 28. TRAJECTORY ASSETS

Prefer procedural trajectory.

Required visual materials:

```text
MAT_Rock_Default
MAT_Rock_Warning
MAT_Rock_Danger
MAT_Rock_Glow
```

Optional trajectory indicator:

```text
VFX_RockTrail
VFX_RockWarning
```

---

# 29. ENVIRONMENT ASSETS

Keep environment simple.

Required:

```text
ENV_Background
ENV_Ground
ENV_SoftLight
ENV_Cloud_01
ENV_Cloud_02
ENV_Decoration_01
```

Optional:

```text
ENV_Tree
ENV_Mountain
ENV_FloatingIsland
ENV_SkyParticle
```

Do not place objects directly over the child's body silhouette.

---

# 30. BODY MOVEMENT UI ASSETS

Setup:

```text
UI_PlayerOutline
UI_CenterZone
UI_MoveLeft
UI_MoveRight
UI_Duck
UI_Jump
UI_FullBodyVisible
UI_PositionOK
UI_MoveBack
UI_MoveForward
```

Gameplay:

```text
UI_AvoidLeft
UI_AvoidRight
UI_DuckPrompt
UI_JumpPrompt
UI_TrackingWarning
UI_Great
```

Icons should be simple and immediately readable.

---

# 31. AUDIO ASSETS

Music:

```text
BGM_AvoidRock_Loop
```

SFX:

```text
SFX_RockSpawn
SFX_RockApproach
SFX_RockWarning
SFX_RockPass
SFX_AvoidSuccess
SFX_SoftHit
SFX_Jump
SFX_Duck
SFX_Complete
```

Voice:

```text
VO_GeserKiri
VO_GeserKanan
VO_Lompat
VO_Menunduk
VO_Hebat
VO_CobaLagi
VO_Selesai
```

---

# 32. VFX ASSETS

Required:

```text
VFX_RockTrail
VFX_RockWarning
VFX_AvoidSuccess
VFX_SoftImpact
VFX_JumpSuccess
VFX_DuckSuccess
VFX_Complete
```

Successful avoid:

```text
ROCK
 ↓
PASS PLAYER
 ↓
✨
```

---

# 33. PERFORMANCE

Use:

- object pooling
- lightweight rock meshes
- lightweight materials
- limited particles
- no unnecessary rigidbody simulation
- deterministic trajectory
- no Instantiate/Destroy loop during gameplay

Preferred:

```text
RockPool.Get()
   ↓
RockController
   ↓
RockPool.Release()
```

---

# 34. SAFETY DESIGN

This is a physical activity game.

SAFE:

- controlled side movement
- body lean
- knee bend
- small jump
- duck

SUPERVISED:

- repeated jumping
- small play area
- nearby furniture

Do not require:

- running
- sprinting
- diving
- falling
- rapid spinning
- extreme bending

Player should have clear floor space.

---

# 35. TRACKING FAILURE

If tracking is lost:

```text
PLAYING
 ↓
TRACKING LOST
 ↓
STOP NEW ROCKS
 ↓
CURRENT ROCK PAUSE / SAFE DESPAWN
 ↓
SHOW GUIDANCE
 ↓
TRACKING RECOVERED
 ↓
RESUME
```

No penalty.

---

# 36. RESULT SYSTEM

Result should focus on participation.

Example:

```text
✨ GREAT JOB! ✨

🪨 Batu dihindari:
18

"Hebat! Kamu bergerak cepat!"
```

Do not expose body-performance ranking.

Optional internal metrics:

```text
rocksSpawned
rocksAvoided
rocksMissed
leftDodges
rightDodges
jumps
ducks
sessionDuration
```

These can be used for analytics but should not become a negative score shown to the child.

---

# 37. SESSION DATA

Example:

```json
{
  "gameId": "motion_avoid_rock",
  "targetDistance": 1.5,
  "sessionDuration": 120,
  "rocksSpawned": 30,
  "rocksAvoided": 25,
  "rocksMissed": 5,
  "leftDodges": 8,
  "rightDodges": 7,
  "jumps": 5,
  "ducks": 5
}
```

---

# 38. MVP

MVP:

```text
1. Camera
2. Position Sync ~1.5m
3. Full-body tracking
4. Calibration
5. Hip tracking
6. Head tracking
7. Knee/foot tracking
8. Side shift detection
9. Duck detection
10. Jump detection
11. Rock spawning
12. Rock approach
13. Left trajectory
14. Right trajectory
15. Low trajectory
16. High trajectory
17. Avoidance validation
18. Feedback
19. 2–3 minute session
20. Result screen
```

---

# 39. ACCEPTANCE CRITERIA

## Position

- [ ] Player can setup around 1.5 m.
- [ ] Full body visible.
- [ ] Position sync provides guidance.
- [ ] Calibration completes.

## Side Movement

- [ ] Left dodge detected.
- [ ] Right dodge detected.
- [ ] Small movement is sufficient.
- [ ] System does not require running.

## Duck

- [ ] Duck baseline calibrated.
- [ ] Head/torso movement detects duck.
- [ ] False positives controlled.

## Jump

- [ ] Jump baseline calibrated.
- [ ] Jump detected reliably.
- [ ] High/low obstacle behavior works.

## Rocks

- [ ] Rock approaches player.
- [ ] Left trajectory works.
- [ ] Right trajectory works.
- [ ] High trajectory works.
- [ ] Low trajectory works.
- [ ] Danger window works.
- [ ] Avoidance validation works.

## Gameplay

- [ ] Difficulty increases gradually.
- [ ] Miss does not cause Game Over.
- [ ] Tracking loss is handled safely.
- [ ] Session lasts approximately 2–3 minutes.

## Safety

- [ ] No running required.
- [ ] No extreme movement required.
- [ ] Jump frequency controlled.
- [ ] Parent can exit anytime.

---

# 40. REUSE FROM MOTION FRAMEWORK

Game 03 should reuse systems from Game 01 and Game 02:

```text
PositionSync
Calibration
BodyTracking
NormalizedBodySpace
JumpDetection
FeedbackSystem
SessionSystem
```

Game 03 adds:

```text
SideShiftDetector
DuckDetector
ObstacleSpawner
RockTrajectory
DangerWindow
AvoidanceValidator
```

---

# 41. MOTION GAME FAMILY ARCHITECTURE

```text
             CAMERA
                ↓
        BODY TRACKING SDK
                ↓
       PLAYER TRACKING LAYER
                ↓
      NORMALIZED BODY SPACE
                ↓
         MOTION FRAMEWORK
                │
       ┌────────┼─────────┐
       ↓        ↓         ↓
   STAR CATCH  FOLLOW   AVOID ROCK
               LINE
       ↓        ↓         ↓
      GAME 01  GAME 02   GAME 03
```

Shared motion layer means future games do not need a new tracking system.

---

# 42. FUTURE EXTENSION

The same avoidance system can later support:

- balls
- clouds
- bubbles
- logs
- falling objects
- flying objects
- moving animals
- magical obstacles

The mechanic remains:

```text
OBJECT TRAJECTORY
       ↓
PLAYER DETECTS
       ↓
BODY RESPONSE
       ↓
SAFE ZONE
```

Only visual assets and trajectory rules change.

---

# 43. DEFINITION OF DONE

AVOID THE ROCK is ready for internal playtest when:

1. Child can position around 1.5 m from camera.
2. Full body is tracked.
3. Side movement is detected.
4. Left dodge works.
5. Right dodge works.
6. Duck works.
7. Jump works.
8. Rocks approach from visible trajectories.
9. Child can avoid rocks without running.
10. Danger window is readable.
11. Avoidance detection feels responsive.
12. Miss is handled without punishment.
13. Tracking loss is handled safely.
14. Session runs for 2–3 minutes.
15. Game produces meaningful light physical movement.
16. Core systems are reusable for future Family Adventure Motion Games.
