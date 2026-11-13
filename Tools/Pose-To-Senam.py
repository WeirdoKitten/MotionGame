"""Convert the pose track of a senam video into a choreography file for the Ikuti Senam game.

Usage:
    python Tools/Pose-To-Senam.py Builds/Senam/Senam-pose.csv --name "Senam Pemanasan"
        [--video Senam.mp4] [--out Assets/_Project/Resources/Motion/Senam/senam-pemanasan.txt]

Input: the CSV from Tools/Video-To-Pose.py (BlazePose landmarks, image coordinates).
Output: a plain text choreography (format below), a report (<out>-report.md next to the CSV) and,
with --video, check pictures: every target drawn back onto the instructor in the original video
(<name>-targets.jpg) and a short overlay video (<name>-overlay.mp4).

Coordinates. The child copies the instructor like a mirror, and the game shows the child's camera
picture mirrored, so a target belongs where the instructor's hand is seen on the screen: points stay
in screen space (x right, y up) and are not flipped. "SL"/"SR" mean the limb seen on the screen-left /
screen-right side (SR hand = the instructor's own left hand = the child's right hand in the mirror).
Points are relative to the instructor's standing hip position (a slow 6 s median, so steps and lunges
still move the targets) and measured in torso lengths (hip centre to shoulder centre).

File format (one record per line, # starts a comment):
    senam 1
    name <text>
    duration <seconds>
    legratio <hip-to-ankle height / torso>      how to scale foot targets to the child's legs
    neutral <SLhand x y> <SRhand x y> <SLfoot x y> <SRfoot x y>
    guide <rate>                                  then g lines: t + 15 joints x y
    g <t> nose SLear SRear SLshoulder SRshoulder SLelbow SRelbow SLwrist SRwrist SLhip SRhip SLknee SRknee SLankle SRankle
    hands <t> <hold> <SLx> <SLy> <SLangle> <SRx> <SRy> <SRangle>   reach with both hands (angle: forearm direction)
    foot <t> <hold> <SL|SR> <x> <y> <raise|step>                    reach with one foot (knee raise or step out)
    head <t> <tilt degrees> <turn>                                  head tilt (+ = toward screen-left) and turn (+ = nose to screen-right)
    march <start> <end> <steps>                                     marching on the spot
    hips <start> <end> <SLx> <SLy> <SRx> <SRy>                      hands on the hips (elbows out); wrist points
    shift <t> <dx>                                                  hips moved sideways (label only)
"""
import argparse
import csv
import os

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
# MediaPipe indices as seen on the screen (the instructor faces the camera, so their right is screen-left).
NOSE, SL_EAR, SR_EAR = 0, 8, 7
SL_SH, SR_SH, SL_EL, SR_EL, SL_WR, SR_WR = 12, 11, 14, 13, 16, 15
SL_HIP, SR_HIP, SL_KN, SR_KN, SL_AN, SR_AN = 24, 23, 26, 25, 28, 27
GUIDE = [NOSE, SL_EAR, SR_EAR, SL_SH, SR_SH, SL_EL, SR_EL, SL_WR, SR_WR, SL_HIP, SR_HIP, SL_KN, SR_KN, SL_AN, SR_AN]

# Detection settings, in torso lengths and seconds.
HAND_FAR = .8          # a hand pose counts when a hand is this far from its resting place
HAND_SAME = .4         # two hand poses closer than this are the same pose
KNEE_RAISE = .35       # ankle this much higher than the other ankle
STEP_OUT = .45         # ankle this far sideways from its resting place
HEAD_TILT, HEAD_TURN = 7.0, .4


def load(path):
    rows = list(csv.DictReader(open(path)))
    t = np.array([float(r["time"]) for r in rows])
    p = np.array([[[float(r[f"x{k}"] or "nan"), float(r[f"y{k}"] or "nan")] for k in range(33)] for r in rows])
    v = np.array([[float(r[f"v{k}"] or 0) for k in range(33)] for r in rows])
    return t, p, v


def smooth(x, width=7):
    kernel = np.ones(width) / width
    pad = np.pad(x, [(width // 2, width // 2)] + [(0, 0)] * (x.ndim - 1), mode="edge")
    return np.apply_along_axis(lambda c: np.convolve(c, kernel, mode="valid"), 0, pad)


def rolling(x, width, fn):
    half = width // 2
    return np.array([fn(x[max(0, i - half):i + half + 1], axis=0) for i in range(len(x))])


def local_extrema(signal, window, mode):
    """Indices where signal is the min (or max) of its +-window neighbourhood."""
    out = []
    for i in range(len(signal)):
        lo, hi = max(0, i - window), min(len(signal), i + window + 1)
        part = signal[lo:hi]
        if (signal[i] == part.min()) if mode == "min" else (signal[i] == part.max()):
            if not out or i - out[-1] > window:
                out.append(i)
    return out


class Senam:
    def __init__(self, t, p, v, aspect):
        self.t, self.v = t, v
        self.fps = 1 / np.median(np.diff(t))
        # Low-confidence joints keep their last good position, then everything is smoothed.
        p = p.copy()
        for k in range(33):
            for i in range(1, len(t)):
                if v[i, k] < .3 or np.isnan(p[i, k, 0]):
                    p[i, k] = p[i - 1, k]
        iso = np.stack([p[..., 0] * aspect, 1 - p[..., 1]], -1)  # equal units both ways, y up
        self.iso = smooth(iso.reshape(len(t), -1)).reshape(len(t), 33, 2)
        hip = (self.iso[:, SL_HIP] + self.iso[:, SR_HIP]) / 2
        shoulder = (self.iso[:, SL_SH] + self.iso[:, SR_SH]) / 2
        self.torso = float(np.median(np.linalg.norm(shoulder - hip, axis=1)))
        self.ref = rolling(hip, int(6 * self.fps) | 1, np.median)
        self.b = (self.iso - self.ref[:, None, :]) / self.torso
        self.hip_offset = (hip - self.ref) / self.torso
        start = t < .5
        self.neutral = {k: self.b[start, k].mean(0) for k in (SL_WR, SR_WR, SL_AN, SR_AN)}
        legs = (hip[:, 1] - (self.iso[:, SL_AN, 1] + self.iso[:, SR_AN, 1]) / 2) / self.torso
        self.legratio = float(np.median(legs))

    def speed(self, k):
        d = np.gradient(self.b[:, k], self.t, axis=0)
        return np.linalg.norm(d, axis=1)

    def hands(self):
        s = smooth(self.speed(SL_WR) + self.speed(SR_WR), 5)
        window = int(.2 * self.fps)
        events = []
        for i in local_extrema(s, window, "min"):
            far = max(np.linalg.norm(self.b[i, k] - self.neutral[k]) for k in (SL_WR, SR_WR))
            if far < HAND_FAR:
                continue
            pose = self.b[i, [SL_WR, SR_WR]]
            if events:
                last = events[-1]
                if self.t[i] - last["end"] < .6 and np.abs(pose - last["pose"]).max() < HAND_SAME:
                    last["end"] = self.t[i]
                    continue
            events.append({"i": i, "t": self.t[i], "end": self.t[i], "pose": pose})
        for e in events:
            # Hold: how long both hands stay near this pose (at most 4 s).
            j = e["i"]
            while j + 1 < len(self.t) and self.t[j + 1] - e["t"] < 4 and \
                    np.abs(self.b[j + 1, [SL_WR, SR_WR]] - e["pose"]).max() < .35:
                j += 1
            e["hold"] = self.t[j] - e["t"]
            e["angles"] = [float(np.degrees(np.arctan2(*(self.b[e["i"], w] - self.b[e["i"], el])[::-1])))
                           for w, el in ((SL_WR, SL_EL), (SR_WR, SR_EL))]
        return events

    def feet(self):
        events = []
        for side, an, other in (("SL", SL_AN, SR_AN), ("SR", SR_AN, SL_AN)):
            lift = self.b[:, an, 1] - self.b[:, other, 1]
            out = np.abs(self.b[:, an, 0] - self.neutral[an][0])
            raised = lift > KNEE_RAISE
            stepped = (out > STEP_OUT) & ~raised
            for mask, kind in ((raised, "raise"), (stepped, "step")):
                i = 0
                while i < len(mask):
                    if not mask[i]:
                        i += 1
                        continue
                    j = i
                    while j + 1 < len(mask) and mask[j + 1]:
                        j += 1
                    if self.t[j] - self.t[i] >= (.15 if kind == "raise" else .3):
                        if kind == "raise":
                            k = i + int(np.argmax(lift[i:j + 1]))
                            events.append(self._foot(side, an, k, self.t[j] - self.t[k], kind))
                        else:
                            # The stance once the foot has landed, and again whenever the foot moves on.
                            k = min(j, i + int(.15 * self.fps))
                            events.append(self._foot(side, an, k, self.t[j] - self.t[k], kind))
                            for m in range(k + 1, j + 1):
                                if np.linalg.norm(self.b[m, an] - self.b[k, an]) > .35:
                                    events[-1]["hold"] = self.t[m] - self.t[k]
                                    k = m
                                    events.append(self._foot(side, an, k, self.t[j] - self.t[k], kind))
                    i = j + 1
        return sorted(events, key=lambda e: e["t"])

    def _foot(self, side, an, i, hold, kind):
        return {"t": self.t[i], "hold": hold, "side": side, "pos": self.b[i, an], "kind": kind}

    def head(self):
        # Screen-space head angles, comparable with the child's mirrored camera picture.
        ear = self.iso[:, SR_EAR] - self.iso[:, SL_EAR]
        shoulders = self.iso[:, SR_SH] - self.iso[:, SL_SH]
        tilt = np.degrees(np.arctan2(ear[:, 1], ear[:, 0]) - np.arctan2(shoulders[:, 1], shoulders[:, 0]))
        tilt = (tilt + 180) % 360 - 180
        mid = (self.iso[:, SL_EAR] + self.iso[:, SR_EAR]) / 2
        turn = (self.iso[:, NOSE, 0] - mid[:, 0]) / np.maximum(1e-3, np.abs(ear[:, 0]))
        self.tilt, self.turn = smooth(tilt, 5), smooth(turn, 5)
        strength = np.maximum(np.abs(self.tilt) / HEAD_TILT, np.abs(self.turn) / HEAD_TURN)
        seen = np.minimum(self.v[:, SL_EAR], self.v[:, SR_EAR]) > .5
        events = []
        for i in local_extrema(strength, int(.3 * self.fps), "max"):
            if strength[i] < 1 or not seen[i]:
                continue
            if events and self.t[i] - events[-1]["t"] < .5:
                continue
            events.append({"t": self.t[i], "tilt": float(self.tilt[i]), "turn": float(self.turn[i])})
        return events

    def march(self, raises):
        lift = self.b[:, SL_AN, 1] - self.b[:, SR_AN, 1]
        win = int(self.fps)
        std = rolling(lift, win | 1, np.std)
        crossings = np.zeros(len(lift))
        sign = np.sign(lift - rolling(lift, win | 1, np.median))
        crossings[1:] = sign[1:] != sign[:-1]
        rate = rolling(crossings, win | 1, np.sum)  # side changes per second
        on = (std > .03) & (std < .3) & (rate >= 2)
        # Side steps also lift the feet in turn; marching keeps the hips and feet in place.
        moving = rolling(np.abs(self.hip_offset[:, 0]), win | 1, np.max) > .3
        out = np.maximum(np.abs(self.b[:, SL_AN, 0] - self.neutral[SL_AN][0]), np.abs(self.b[:, SR_AN, 0] - self.neutral[SR_AN][0]))
        on &= ~moving & (rolling(out, win | 1, np.max) < STEP_OUT)
        for e in raises:
            on[np.abs(self.t - e["t"]) < .8] = False
        segments, i = [], 0
        while i < len(on):
            if not on[i]:
                i += 1
                continue
            j = i
            while j + 1 < len(on) and (on[j + 1] or (np.any(on[j + 1:j + 1 + win]) and True)):
                j += 1
            if self.t[j] - self.t[i] >= 2:
                segments.append({"start": self.t[i], "end": self.t[j], "steps": int(crossings[i:j + 1].sum())})
            i = j + 1
        return segments

    def hips(self):
        """Hands on the hips: wrists at hip height near the waist, elbows out to the sides."""
        on = np.ones(len(self.t), bool)
        for wr, el, sign in ((SL_WR, SL_EL, -1), (SR_WR, SR_EL, 1)):
            w, e = self.b[:, wr], self.b[:, el]
            on &= (w[:, 1] > .1) & (w[:, 1] < .6) & (np.abs(w[:, 0]) > .25) & (np.abs(w[:, 0]) < .7)
            on &= (e[:, 0] * sign > .6) & (e[:, 1] > .45)
        segments, i = [], 0
        while i < len(on):
            if not on[i]:
                i += 1
                continue
            j = i
            while j + 1 < len(on) and (on[j + 1] or np.any(on[j + 1:j + 1 + int(.5 * self.fps)])):
                j += 1
            if self.t[j] - self.t[i] >= 1:
                mid = slice(i, j + 1)
                segments.append({"start": self.t[i], "end": self.t[j],
                                 "SL": np.median(self.b[mid, SL_WR], 0), "SR": np.median(self.b[mid, SR_WR], 0)})
            i = j + 1
        return segments

    def shifts(self):
        dx = smooth(self.hip_offset[:, 0], 9)
        events = []
        for i in local_extrema(np.abs(dx), int(.3 * self.fps), "max"):
            if abs(dx[i]) > .4:
                events.append({"t": self.t[i], "dx": float(dx[i])})
        return events


def fmt(x):
    return f"{x:.3f}"


def write(path, name, senam, hands, feet, heads, marches, shifts, hips):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    n = senam.neutral
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("senam 1\n")
        f.write("# Made by Tools/Pose-To-Senam.py from a BlazePose track. Points: screen space (x right, y up), "
                "relative to the standing hip, in torso lengths.\n")
        f.write(f"name {name}\nduration {senam.t[-1]:.2f}\nlegratio {senam.legratio:.3f}\n")
        f.write("neutral " + " ".join(fmt(c) for k in (SL_WR, SR_WR, SL_AN, SR_AN) for c in n[k]) + "\n")
        rate = 15
        f.write(f"guide {rate}\n")
        step = max(1, round(senam.fps / rate))
        for i in range(0, len(senam.t), step):
            f.write(f"g {senam.t[i]:.3f} " + " ".join(fmt(c) for k in GUIDE for c in senam.b[i, k]) + "\n")
        records = []
        for e in hands:
            sl, sr = e["pose"]
            records.append((e["t"], f"hands {e['t']:.3f} {e['hold']:.2f} {fmt(sl[0])} {fmt(sl[1])} {e['angles'][0]:.0f} "
                                    f"{fmt(sr[0])} {fmt(sr[1])} {e['angles'][1]:.0f}"))
        for e in feet:
            records.append((e["t"], f"foot {e['t']:.3f} {e['hold']:.2f} {e['side']} {fmt(e['pos'][0])} {fmt(e['pos'][1])} {e['kind']}"))
        for e in heads:
            records.append((e["t"], f"head {e['t']:.3f} {e['tilt']:.1f} {e['turn']:.2f}"))
        for e in marches:
            records.append((e["start"], f"march {e['start']:.3f} {e['end']:.3f} {e['steps']}"))
        for e in shifts:
            records.append((e["t"], f"shift {e['t']:.3f} {e['dx']:.2f}"))
        for e in hips:
            records.append((e["start"], f"hips {e['start']:.3f} {e['end']:.3f} {fmt(e['SL'][0])} {fmt(e['SL'][1])} {fmt(e['SR'][0])} {fmt(e['SR'][1])}"))
        for _, line in sorted(records, key=lambda r: r[0]):
            f.write(line + "\n")


def report(path, name, senam, hands, feet, heads, marches, shifts, hips):
    duration = senam.t[-1]
    covered = np.zeros(len(senam.t), bool)
    for e in hands:
        covered |= (senam.t >= e["t"] - .5) & (senam.t <= e["t"] + e["hold"] + .5)
    for e in feet:
        covered |= (senam.t >= e["t"] - .5) & (senam.t <= e["t"] + e["hold"] + .5)
    for e in heads:
        covered |= np.abs(senam.t - e["t"]) <= .5
    for e in marches + hips:
        covered |= (senam.t >= e["start"]) & (senam.t <= e["end"])
    lines = [f"# Konversi senam: {name}", "",
             f"Durasi {duration:.1f} s, {len(senam.t)} frame ({senam.fps:.0f} fps). Panjang badan (pinggul-bahu) "
             f"{senam.torso:.3f} unit gambar; rasio kaki/badan {senam.legratio:.2f}.", "",
             "| Gerakan dasar | Jumlah | Keterangan |", "|---|---|---|",
             f"| Menjangkau dua tangan (hands) | {len(hands)} | pose tangan kunci, target untuk kedua tangan |",
             f"| Angkat lutut (foot raise) | {sum(e['kind'] == 'raise' for e in feet)} | target kaki terangkat |",
             f"| Langkah/kangkang (foot step) | {sum(e['kind'] == 'step' for e in feet)} | target kaki ke samping |",
             f"| Gerak kepala (head) | {len(heads)} | tekuk/tengok kepala |",
             f"| Jalan di tempat (march) | {len(marches)} segmen, {sum(m['steps'] for m in marches)} langkah | "
             f"{', '.join(f'{m['start']:.0f}-{m['end']:.0f} s' for m in marches)} |",
             f"| Tangan di pinggang (hips) | {len(hips)} segmen | {', '.join(f'{m['start']:.0f}-{m['end']:.0f} s' for m in hips)} |",
             f"| Geser badan (shift, label) | {len(shifts)} | sudah terwakili posisi target tangan/kaki |", "",
             f"Waktu yang tercakup oleh setidaknya satu gerakan dasar: {100 * covered.mean():.0f}%.", "",
             "## Daftar pose tangan", "", "| t (s) | tahan (s) | tangan kiri layar (x, y) | tangan kanan layar (x, y) |", "|---|---|---|---|"]
    for e in hands:
        sl, sr = e["pose"]
        lines.append(f"| {e['t']:.2f} | {e['hold']:.2f} | {sl[0]:.2f}, {sl[1]:.2f} | {sr[0]:.2f}, {sr[1]:.2f} |")
    open(path, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    return covered.mean()


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("pose_csv")
    parser.add_argument("--name", default="Senam")
    parser.add_argument("--aspect", type=float, default=16 / 9, help="video width / height")
    parser.add_argument("--out", default=os.path.join(ROOT, "Assets", "_Project", "Resources", "Motion", "Senam", "senam-pemanasan.txt"))
    parser.add_argument("--video", help="original video, for the check pictures")
    args = parser.parse_args()

    t, p, v = load(args.pose_csv)
    senam = Senam(t, p, v, args.aspect)
    hands, feet, heads = senam.hands(), senam.feet(), senam.head()
    marches = senam.march([e for e in feet if e["kind"] == "raise"])
    shifts, hips = senam.shifts(), senam.hips()
    write(args.out, args.name, senam, hands, feet, heads, marches, shifts, hips)
    base = os.path.splitext(args.pose_csv)[0].replace("-pose", "")
    share = report(base + "-report.md", args.name, senam, hands, feet, heads, marches, shifts, hips)
    print(f"hands {len(hands)}, feet {len(feet)} (raise {sum(e['kind'] == 'raise' for e in feet)}), head {len(heads)}, "
          f"march {len(marches)}, hips {len(hips)}, shift {len(shifts)}; covered {100 * share:.0f}% of the time; out {args.out}")
    if args.video:
        import importlib.util
        spec = importlib.util.spec_from_file_location("senam_check", os.path.join(os.path.dirname(__file__), "Senam-Check.py"))
        check = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(check)
        check.run(args.video, args.out, senam, base)


if __name__ == "__main__":
    main()
