# Family Adventure — Motion Game 02
# FOLLOW THE LINE
## Mechanics + Assets Specification for Unity 3D

**Version:** 1.0  
**Status:** Implementation Draft  
**Game Type:** Full-Body Motion / Light Physical Activity  
**Player:** 1 Child  
**Camera Distance:** ~1.5 m  
**Primary Body Inputs:** Hands + Knees + Feet + Jump  
**Session Target:** 2–3 minutes

---

# 1. GAME CONCEPT

**FOLLOW THE LINE** adalah motion game olahraga ringan untuk anak.

Anak berdiri di depan kamera sekitar 1,5 meter. Sebuah garis/pola muncul di area gameplay. Anak harus mengikuti garis tersebut menggunakan bagian tubuh yang ditentukan:

- ✋ tangan kiri
- ✋ tangan kanan
- 🤲 kedua tangan
- 🦵 dengkul kiri
- 🦵 dengkul kanan
- 🦵🦵 kedua kaki/dengkul
- 👣 kaki
- ✋ + 🦵 kombinasi tangan dan kaki
- ⬆️ lompatan

Fokus game:

> **Coordination + Balance + Body Control + Physical Movement**

Game bukan menggambar garis dengan mouse/controller. Anak benar-benar menggunakan gerakan tubuh.

---

# 2. CORE GAMEPLAY

```text
SEE THE LINE
     ↓
UNDERSTAND THE MOVEMENT
     ↓
MOVE BODY PART
     ↓
FOLLOW THE LINE
     ↓
COMPLETE PATH
     ↓
CELEBRATE
     ↓
NEXT MOVEMENT
```

Core instruction:

> **"Ikuti garisnya!"**

Untuk bagian tubuh:

> **"Gunakan tanganmu!"**

> **"Gunakan kakimu!"**

> **"Angkat lututmu!"**

> **"Lompat!"**

---

# 3. PHYSICAL SETUP

## 3.1 Camera Distance

Reference:

**1.50 m**

Recommended setup zone:

**1.35–1.65 m**

1.5 m adalah reference design distance, bukan pengukuran absolut yang harus presisi sampai sentimeter.

## 3.2 Player Position

Anak berdiri menghadap kamera.

```text
              CAMERA
                 📷
                  |
                  |
                1.5m
                  |
                  ▼
           ┌────────────┐
           │    ✋ ✋    │
           │      O     │
           │     /|\    │
           │      |     │
           │     / \    │
           │    🦵 🦵   │
           │     👣     │
           └────────────┘
```

Anak tidak perlu berjalan keluar dari play area.

---

# 4. TRACKING REQUIREMENTS

Minimum tracking points:

```text
Head
Shoulders
Hip
Left Hand
Right Hand
Left Knee
Right Knee
Left Foot
Right Foot
```

Tracking pipeline:

```text
Camera
 ↓
Body Tracking SDK
 ↓
Tracking Adapter
 ↓
Normalized Body Space
 ↓
Line Interaction System
```

Gameplay tidak boleh bergantung langsung pada SDK tertentu.

---

# 5. POSITION SYNC

Sebelum game:

```text
PLAYER DETECT
    ↓
FULL BODY CHECK
    ↓
POSITION CHECK
    ↓
VALID?
    ├── NO → GUIDE PLAYER
    └── YES
          ↓
      CALIBRATION
```

Messages:

Too close:

> "Mundur sedikit."

Too far:

> "Maju sedikit."

Body not visible:

> "Pastikan seluruh tubuh terlihat."

Valid:

> "Bagus! Posisimu sudah pas."

---

# 6. CALIBRATION

Calibration dilakukan per player.

## 6.1 Standing Baseline

Record:

```text
headPosition
shoulderPosition
hipPosition
leftHandPosition
rightHandPosition
leftKneePosition
rightKneePosition
leftFootPosition
rightFootPosition
bodyHeight
bodyWidth
```

## 6.2 Movement Range

Record range sederhana:

```text
handReach
kneeRaise
footReach
jumpThreshold
```

Semua threshold sebaiknya relatif terhadap ukuran tubuh player.

---

# 7. NORMALIZED BODY SPACE

Gameplay menggunakan koordinat relatif.

Horizontal:

```text
LEFT        CENTER        RIGHT
 -1           0            +1
```

Vertical:

```text
HIGH        +1
MID          0
LOW        -1
```

Body points dipetakan ke ruang gameplay.

Contoh:

```text
leftHand  → (-0.6, +0.2)
rightHand → (+0.6, +0.2)
leftKnee  → (-0.3, -0.5)
rightKnee → (+0.3, -0.5)
```

Nilai tersebut adalah konsep normalized space, bukan fixed screen pixels.

---

# 8. LINE SYSTEM

Garis adalah **path**, bukan sekadar visual.

Setiap line memiliki:

```text
LinePath
├── Start Point
├── Control Points
├── End Point
├── Width
├── Tolerance
├── Required Body Part
└── Difficulty
```

---

# 9. LINE VISUAL STATES

Line memiliki tiga visual state.

## 9.1 Inactive

```text
────────────
```

## 9.2 Active

```text
══════──────
```

Bagian yang sedang harus diikuti terlihat aktif.

## 9.3 Completed

```text
════════════
✨
```

---

# 10. TOLERANCE CORRIDOR

Anak tidak harus tepat berada di tengah garis.

Gunakan tolerance corridor.

```text
     OUT
      ↓
┌─────────────────────┐
│    TOLERANCE ZONE   │
│ ─────────────────── │
│       LINE          │
│ ─────────────────── │
└─────────────────────┘
```

Jika body point berada di dalam corridor:

`ON_LINE = TRUE`

Jika keluar:

`OFF_LINE = TRUE`

Game memberikan guidance, bukan punishment.

---

# 11. PATH PROGRESS

Progress tidak dihitung hanya berdasarkan jarak ke endpoint.

Gunakan posisi terdekat pada path:

```text
START ●══════════──────● END
                 ↑
             BODY POINT
```

Sistem mencari:

`nearestPathDistance`

Kemudian:

```text
progress =
nearestPathDistance / totalPathLength
```

Progress:

`0.0 → 1.0`

---

# 12. BODY PART MODES

## MODE 01 — LEFT HAND

```text
●──────────────●
      ✋
```

Anak mengikuti garis dengan tangan kiri.

## MODE 02 — RIGHT HAND

```text
●──────────────●
            ✋
```

## MODE 03 — TWO HANDS

```text
        ●
       / \
      /   \
     ●     ●

    ✋     ✋
```

Masing-masing tangan mengikuti path masing-masing.

## MODE 04 — LEFT KNEE

```text
●────────●

🦵
👦
```

## MODE 05 — RIGHT KNEE

```text
●────────●

        🦵
         👦
```

## MODE 06 — TWO KNEES

```text
●────────────●

    🦵  🦵
      👦
```

## MODE 07 — FOOT

Gunakan foot landmark untuk mengikuti lower path.

## MODE 08 — HAND + KNEE

Satu path dapat meminta:

```text
✋ → LEFT SECTION
🦵 → RIGHT SECTION
```

## MODE 09 — JUMP

Path berada di area high.

Player harus mencapai high zone melalui jump.

---

# 13. LINE PATTERNS

## 13.1 Straight

```text
●──────────────●
```

## 13.2 Horizontal

```text
●══════════════●
```

## 13.3 Vertical

```text
●
│
│
│
●
```

## 13.4 Diagonal

```text
●
 ╲
  ╲
   ╲
    ●
```

## 13.5 L Shape

```text
●──────╮
       │
       ●
```

## 13.6 ZIGZAG

```text
●──╲
    ╲──╱
       ╲──●
```

## 13.7 WAVE

```text
●~~~╮
    ╰~~~╮
         ╰~~~●
```

## 13.8 ARC

```text
●
 ╲
  ╲
   ╰────●
```

## 13.9 CIRCLE

```text
    ╭────╮
   /      \
  │        │
   \      /
    ╰────╯
```

## 13.10 SPIRAL

```text
    ╭────╮
   / ╭──╮ \
  │  │  │  │
   \ ╰──╯ /
    ╰────╯
```

---

# 14. SPORTS MOVEMENT DESIGN

Game harus menghasilkan gerakan tubuh.

Contoh sequence:

```text
1. Reach Left
2. Reach Right
3. Raise Left Knee
4. Raise Right Knee
5. Both Hands Up
6. Both Knees
7. Hand + Foot
8. Jump
```

Gerakan dilakukan bergantian agar tidak membuat anak terlalu cepat lelah.

---

# 15. GAMEPLAY ROUND STRUCTURE

Setiap round:

```text
SHOW BODY PART
      ↓
SHOW START POINT
      ↓
COUNTDOWN / READY
      ↓
LINE ACTIVE
      ↓
PLAYER FOLLOWS
      ↓
PATH COMPLETE
      ↓
CELEBRATION
      ↓
NEXT ROUND
```

---

# 16. SAMPLE GAMEPLAY

## Round 01 — Left Hand

UI:

> "Tangan kiri!"

Line:

```text
●──────────────●
```

Player:

```text
✋ → → → →
```

Complete.

---

## Round 02 — Right Hand

```text
●
 ╲
  ╲
   ╲
    ●
```

Player follows diagonal using right hand.

---

## Round 03 — Left Knee

```text
●──────╮
       ╰────●
```

Player raises left knee following path.

---

## Round 04 — Right Knee

Opposite side.

---

## Round 05 — Two Hands

```text
       ●
      / \
     /   \
    ●     ●
```

Both hands move simultaneously.

---

## Round 06 — Two Knees

Low horizontal path.

---

## Round 07 — Hand + Knee

Path contains two movement sections.

```text
HAND SECTION
     ↓
KNEE SECTION
```

Player transitions naturally.

---

## Round 08 — Jump

High path.

```text
          ●
         /
        /
       ●

       👦
       ↑
      JUMP
```

---

# 17. DIFFICULTY PROGRESSION

## LEVEL 1 — BASIC REACH

Only:

- left hand
- right hand

Simple straight lines.

## LEVEL 2 — LOWER BODY

Add:

- left knee
- right knee

## LEVEL 3 — PATTERN

Add:

- diagonal
- L
- zigzag

## LEVEL 4 — TWO BODY PARTS

Add:

- two hands
- two knees

## LEVEL 5 — FULL BODY

Add:

- hand + knee
- hand + foot
- jump

## LEVEL 6 — MIXED

Random controlled sequence:

```text
HAND
KNEE
HAND
TWO HANDS
KNEE
JUMP
TWO KNEES
```

---

# 18. DIFFICULTY PARAMETERS

```text
LineDifficultyConfig
├── lineLength
├── lineComplexity
├── tolerance
├── movementSpeed
├── bodyPartComplexity
├── simultaneousInput
└── jumpFrequency
```

Do not increase all variables simultaneously.

---

# 19. SUCCESS CONDITION

A line is complete when:

1. Player selects the required body part.
2. Body point enters the path corridor.
3. Player reaches sufficient path progress.
4. Player reaches endpoint.
5. Required body-part conditions are satisfied.

Suggested:

```text
progress >= 0.95
```

with appropriate tolerance.

---

# 20. FAILURE / RECOVERY

If body leaves path:

```text
OFF LINE
 ↓
GUIDANCE
 ↓
RETURN TO PATH
```

Message:

> "Ikuti garisnya!"

No health loss.

No Game Over.

No negative score.

---

# 21. FEEDBACK

Successful line:

```text
LINE
 ↓
GLOW
 ↓
✨ PARTICLES
 ↓
SFX
 ↓
NEXT LINE
```

Messages:

> "Hebat!"

> "Bagus!"

> "Mantap!"

Feedback harus singkat supaya tidak memutus physical flow.

---

# 22. UNITY SCENE ARCHITECTURE

```text
MotionFollowLine
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
│   ├── HandTracker
│   ├── KneeTracker
│   ├── FootTracker
│   ├── JumpDetector
│   └── GestureManager
│
├── LineSystem
│   ├── LinePath
│   ├── LineRenderer
│   ├── LineProgressTracker
│   ├── LineToleranceDetector
│   └── LineSequenceManager
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
MotionFollowLineGameManager.cs
MotionCameraController.cs

PlayerTrackingAdapter.cs
BodyTrackingProvider.cs

PositionSyncController.cs

CalibrationManager.cs
CalibrationData.cs

NormalizedBodySpace.cs

HandTracker.cs
KneeTracker.cs
FootTracker.cs
JumpDetector.cs
GestureManager.cs

LinePath.cs
LinePathPoint.cs
LineRendererController.cs
LineProgressTracker.cs
LineToleranceDetector.cs
LineSequenceManager.cs
LineBodyPartRequirement.cs

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

# 24. LINE DATA STRUCTURE

Example:

```json
{
  "lineId": "line_001",
  "bodyPart": "LEFT_HAND",
  "pattern": "STRAIGHT",
  "difficulty": 1,
  "tolerance": 0.18,
  "requiredProgress": 0.95
}
```

Two hands:

```json
{
  "lineId": "line_010",
  "bodyPart": "TWO_HANDS",
  "pattern": "V_SHAPE",
  "difficulty": 4,
  "tolerance": 0.2,
  "requiredProgress": 0.95
}
```

Knee:

```json
{
  "lineId": "line_020",
  "bodyPart": "LEFT_KNEE",
  "pattern": "HORIZONTAL",
  "difficulty": 2,
  "tolerance": 0.22,
  "requiredProgress": 0.95
}
```

Jump:

```json
{
  "lineId": "line_030",
  "bodyPart": "JUMP",
  "pattern": "HIGH_ARC",
  "difficulty": 5,
  "tolerance": 0.2,
  "requiredProgress": 0.95
}
```

---

# 25. LINE ASSET SYSTEM

Lines should preferably be **procedural/data-driven**, not individual baked textures.

Recommended:

```text
LinePath
    ↓
LineRenderer / Mesh
    ↓
Material
    ↓
Glow / Progress Shader
```

This allows many patterns without creating hundreds of image assets.

---

# 26. REQUIRED VISUAL ASSETS

## 26.1 Line Assets

If using procedural rendering:

```text
MAT_Line_Default
MAT_Line_Active
MAT_Line_Completed
MAT_Line_Glow
MAT_Line_Warning
```

Optional decorative sprites:

```text
SPR_Line_Start
SPR_Line_End
SPR_Line_Checkpoint
```

## 26.2 Body-Part Icons

Required:

```text
ICON_LeftHand
ICON_RightHand
ICON_BothHands

ICON_LeftKnee
ICON_RightKnee
ICON_BothKnees

ICON_LeftFoot
ICON_RightFoot
ICON_BothFeet

ICON_HandFoot
ICON_Jump
```

Style:

- simple silhouette
- child friendly
- immediately readable
- no detailed anatomy
- no character face

---

# 27. GAMEPLAY VISUAL ASSETS

Required:

```text
VFX_LineStart
VFX_LineProgress
VFX_LineComplete
VFX_LineFailSoft
VFX_JumpSuccess
VFX_FullBodySuccess
```

Optional:

```text
VFX_Sparkle
VFX_StarBurst
VFX_GlowPulse
```

---

# 28. ENVIRONMENT ASSETS

Keep the environment visually clean.

Required:

```text
ENV_Background
ENV_Ground
ENV_SoftLight
ENV_FloatingShape_01
ENV_FloatingShape_02
ENV_Decoration_01
```

Optional:

```text
ENV_Cloud
ENV_Star
ENV_Planet
ENV_Rainbow
```

Do not place busy objects behind the child silhouette.

---

# 29. PLAYER GUIDE ASSETS

Setup:

```text
UI_PlayerOutline
UI_FullBodyFrame
UI_PositionCenter
UI_MoveForward
UI_MoveBackward
UI_HandsVisible
UI_FeetVisible
UI_Ready
```

Gameplay:

```text
UI_BodyPartPrompt
UI_FollowLinePrompt
UI_ReturnToLine
UI_Great
UI_Complete
```

---

# 30. AUDIO ASSETS

Music:

```text
BGM_FollowTheLine_Loop
```

SFX:

```text
SFX_LineAppear
SFX_LineStart
SFX_LineProgress
SFX_LineComplete
SFX_SoftOffLine
SFX_Jump
SFX_Great
SFX_Complete
```

Voice prompts:

```text
VO_TanganKiri
VO_TanganKanan
VO_KeduaTangan

VO_LututKiri
VO_LututKanan
VO_KeduaLutut

VO_KakiKiri
VO_KakiKanan
VO_Lompat

VO_IkutiGaris
VO_CobaLagi
VO_Hebat
```

Voice can use recorded human voice assets. Voice cloning is not required.

---

# 31. ASSET NAMING CONVENTION

Recommended:

```text
PF_       Prefab
M_        Material
MAT_      Material
TEX_      Texture
SPR_      Sprite
ICON_     UI icon
VFX_      Visual effect
SFX_      Sound effect
VO_       Voice
BGM_      Music
ENV_      Environment
UI_       UI asset
```

Example:

```text
PF_Line_Default
PF_Line_TwoHand
PF_Line_Jump

MAT_Line_Active

ICON_LeftHand
ICON_RightHand
ICON_LeftKnee
ICON_RightKnee
ICON_Jump

VFX_LineComplete
SFX_LineComplete
```

---

# 32. PERFORMANCE

Use:

- procedural line generation where possible
- object pooling for VFX
- simple meshes
- lightweight materials
- limited particle counts
- no unnecessary physics
- no Instantiate/Destroy loops during gameplay

Line geometry should be generated once per sequence and reused.

---

# 33. SAFETY

Game is physical activity.

SAFE:

- standing
- reaching
- knee raise
- controlled foot movement
- small jump

SUPERVISED:

- repeated jumping
- limited room
- nearby furniture

Avoid requiring:

- running
- diving
- falling
- rapid spinning
- floor exercises
- dangerous stretching

Parent should provide a clear, stable play area.

---

# 34. TRACKING LOSS

If tracking confidence drops:

```text
PLAYING
 ↓
TRACKING LOST
 ↓
STOP NEW LINE
 ↓
SHOW GUIDANCE
 ↓
TRACKING RECOVERED
 ↓
RESUME
```

No penalty.

---

# 35. MOTION FRAMEWORK REUSE

Game 02 must reuse the Motion Framework established by Game 01 STAR CATCH.

Shared systems:

```text
PositionSync
Calibration
BodyTracking
NormalizedBodySpace
HandTracking
KneeTracking
FootTracking
JumpDetection
GestureManager
FeedbackSystem
```

Game 02 adds:

```text
LinePathSystem
LineProgressTracker
LineToleranceDetector
LineSequenceManager
BodyPartRequirement
```

Architecture:

```text
             CAMERA
                ↓
        BODY TRACKING SDK
                ↓
      PLAYER TRACKING ADAPTER
                ↓
       NORMALIZED BODY SPACE
                ↓
          MOTION ENGINE
          /     |      \
       HAND   KNEE     FOOT
          \     |      /
           \    |     /
            LINE SYSTEM
                ↓
          FOLLOW THE LINE
```

---

# 36. MVP

MVP should include:

```text
1. Position setup ~1.5m
2. Full-body tracking
3. Calibration
4. Left hand
5. Right hand
6. Left knee
7. Right knee
8. Feet
9. Jump
10. Straight line
11. Diagonal line
12. Zigzag line
13. Line tolerance
14. Path progress
15. Success feedback
16. Soft miss handling
17. 2–3 minute session
18. Result screen
```

---

# 37. ACCEPTANCE CRITERIA

## Position

- [ ] Player can position approximately 1.5m from camera.
- [ ] Full body is visible.
- [ ] Setup guidance works.
- [ ] Calibration works.

## Body

- [ ] Left hand tracked.
- [ ] Right hand tracked.
- [ ] Left knee tracked.
- [ ] Right knee tracked.
- [ ] Feet tracked.
- [ ] Jump detected.

## Line

- [ ] Line appears correctly.
- [ ] Line has start and endpoint.
- [ ] Required body part is displayed.
- [ ] Tolerance corridor works.
- [ ] Progress works.
- [ ] Completion works.

## Gameplay

- [ ] Player can follow line using hands.
- [ ] Player can follow line using knees.
- [ ] Player can use feet.
- [ ] Two-hand patterns work.
- [ ] Two-knee patterns work.
- [ ] Jump patterns work.
- [ ] Mixed patterns work.
- [ ] Difficulty progresses.

## Safety

- [ ] No running required.
- [ ] Jump frequency controlled.
- [ ] No punishment for missing.
- [ ] Tracking loss is safely handled.
- [ ] Player can exit anytime.

---

# 38. DEFINITION OF DONE

FOLLOW THE LINE is ready for internal playtest when:

1. Child can complete position setup.
2. Child can calibrate successfully.
3. Child can understand the body-part prompt without developer explanation.
4. Hand line following works.
5. Knee line following works.
6. Foot line following works.
7. Two-hand interaction works.
8. Two-knee interaction works.
9. Jump interaction works.
10. Line progress feels responsive.
11. Tolerance prevents unnecessary frustration.
12. Visual/audio feedback is immediate.
13. Session can run continuously for 2–3 minutes.
14. Tracking loss does not break the session.
15. Game provides light physical activity without requiring running.
16. Motion systems are reusable by future Family Adventure games.
