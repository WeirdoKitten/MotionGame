"""Extract BlazePose landmarks from a video, using the same ONNX models as the game.

Usage:
    python Tools/Video-To-Pose.py Senam.mp4 [--out Builds/Senam] [--width 1280]

Writes <name>-pose.csv (one row per frame, 33 landmarks) and <name>-sheet.jpg (frames with
the skeleton drawn, every few seconds) for checking the result by eye.

The pipeline mirrors PoseTracker.cs (BlazePoseTracker on branch wip/gerakan-baru): the person
detector finds the body, the landmark model runs on a rotated crop around it, and later frames
reuse the crop from the previous frame's auxiliary landmarks until the pose flag says the body
is gone. Coordinates in the CSV are image coordinates normalized to 0..1 (x right, y down),
not mirrored.
"""
import argparse
import csv
import math
import os

import cv2
import numpy as np
import onnxruntime as ort

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODELS = os.path.join(ROOT, "Assets", "_Project", "Resources", "Motion", "Pose")
DETECTOR, LANDMARKER = 224, 256
BODY_CENTRE, BODY_TOP = 33, 34
EDGES = [(11, 12), (11, 13), (13, 15), (12, 14), (14, 16), (11, 23), (12, 24), (23, 24), (23, 25), (25, 27),
         (24, 26), (26, 28), (27, 29), (29, 31), (27, 31), (28, 30), (30, 32), (28, 32), (15, 19), (16, 20),
         (0, 11), (0, 12)]


def sigmoid(x):
    return 1 / (1 + math.exp(-max(-30.0, min(30.0, x))))


class BlazePose:
    def __init__(self):
        opts = ort.SessionOptions()
        opts.log_severity_level = 3
        self.detector = ort.InferenceSession(os.path.join(MODELS, "pose_detection.onnx"), opts, providers=["CPUExecutionProvider"])
        self.landmarker = ort.InferenceSession(os.path.join(MODELS, "pose_landmarks_lite.onnx"), opts, providers=["CPUExecutionProvider"])
        self.anchors = np.loadtxt(os.path.join(MODELS, "anchors.csv"), delimiter=",", dtype=np.float32)[:, :2]
        self.track = None  # (hip, top) in image pixels from the previous frame

    def detect(self, rgb):
        h, w = rgb.shape[:2]
        side = max(w, h)
        scale = side / DETECTOR
        m = np.array([[scale, 0, (w - side) / 2], [0, scale, (h - side) / 2]], np.float32)
        crop = cv2.warpAffine(rgb, m, (DETECTOR, DETECTOR), flags=cv2.INTER_LINEAR | cv2.WARP_INVERSE_MAP)
        boxes, scores = self.detector.run(None, {"input_1": crop[None]})
        best = int(np.argmax(scores[0, :, 0]))
        if sigmoid(float(scores[0, best, 0])) < .5:
            return None
        anchor = self.anchors[best] * DETECTOR
        hip = m @ np.append(anchor + boxes[0, best, 4:6], 1)
        top = m @ np.append(anchor + boxes[0, best, 6:8], 1)
        return hip, top

    def landmarks(self, rgb, hip, top):
        delta = top - hip
        length = float(np.hypot(*delta))
        radius = 1.25 * length
        if radius < 4:
            return None
        # The body axis (hip to top) becomes "up" in the crop.
        sn, cs = delta[0] / length, -delta[1] / length
        s = radius / (LANDMARKER / 2)
        half = LANDMARKER / 2
        m = np.array([[s * cs, -s * sn, hip[0] - s * (cs * half - sn * half)],
                      [s * sn, s * cs, hip[1] - s * (sn * half + cs * half)]], np.float32)
        crop = cv2.warpAffine(rgb, m, (LANDMARKER, LANDMARKER), flags=cv2.INTER_LINEAR | cv2.WARP_INVERSE_MAP)
        out = self.landmarker.run(["Identity", "Identity_1"], {"input_1": crop[None]})
        points, flag = out[0].reshape(39, 5), float(out[1][0, 0])
        if flag < .5:
            return None
        image = np.c_[points[:, :2], np.ones(39)] @ m.T
        seen = np.array([sigmoid(min(p[3], p[4])) for p in points])
        depth = points[:, 2] * s
        return image, seen, depth, flag

    def run(self, rgb):
        """Returns (points px 33x2, seen 33, depth 33, presence, tracked) or None."""
        result, tracked = None, False
        if self.track is not None:
            result = self.landmarks(rgb, *self.track)
            tracked = result is not None
        if result is None:
            found = self.detect(rgb)
            if found is not None:
                result = self.landmarks(rgb, *found)
        if result is None:
            self.track = None
            return None
        image, seen, depth, flag = result
        self.track = (image[BODY_CENTRE], image[BODY_TOP])
        return image[:33], seen[:33], depth[:33], flag, tracked


def draw(frame, points, seen):
    for a, b in EDGES:
        if seen[a] > .5 and seen[b] > .5:
            cv2.line(frame, tuple(int(v) for v in points[a]), tuple(int(v) for v in points[b]), (60, 255, 120), 3, cv2.LINE_AA)
    for k in (15, 16, 19, 20, 27, 28, 31, 32):
        if seen[k] > .5:
            cv2.circle(frame, tuple(int(v) for v in points[k]), 7, (0, 220, 255) if k < 23 else (255, 180, 60), -1, cv2.LINE_AA)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("video")
    parser.add_argument("--out", default=os.path.join(ROOT, "Builds", "Senam"))
    parser.add_argument("--width", type=int, default=1280, help="frames are resized to this width before the models run")
    parser.add_argument("--sheet-every", type=float, default=6.0, help="seconds between frames on the contact sheet")
    args = parser.parse_args()

    os.makedirs(args.out, exist_ok=True)
    name = os.path.splitext(os.path.basename(args.video))[0]
    video = cv2.VideoCapture(args.video)
    fps = video.get(cv2.CAP_PROP_FPS) or 30
    total = int(video.get(cv2.CAP_PROP_FRAME_COUNT))
    pose = BlazePose()
    tiles, next_tile = [], 0.0
    found_count = tracked_count = 0
    with open(os.path.join(args.out, name + "-pose.csv"), "w", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(["frame", "time", "found", "tracked", "presence"] +
                        [f"{c}{k}" for k in range(33) for c in ("x", "y", "z", "v")])
        index = 0
        while True:
            ok, frame = video.read()
            if not ok:
                break
            h, w = frame.shape[:2]
            small = cv2.resize(frame, (args.width, round(h * args.width / w)), interpolation=cv2.INTER_AREA)
            rgb = cv2.cvtColor(small, cv2.COLOR_BGR2RGB).astype(np.float32) / 255
            time = index / fps
            result = pose.run(rgb)
            sh, sw = small.shape[:2]
            if result is None:
                writer.writerow([index, f"{time:.3f}", 0, 0, 0] + [""] * 132)
            else:
                points, seen, depth, flag, tracked = result
                found_count += 1
                tracked_count += tracked
                row = [index, f"{time:.3f}", 1, int(tracked), f"{flag:.3f}"]
                for k in range(33):
                    row += [f"{points[k, 0] / sw:.4f}", f"{points[k, 1] / sh:.4f}", f"{depth[k] / sw:.4f}", f"{seen[k]:.3f}"]
                writer.writerow(row)
            if time >= next_tile:
                next_tile += args.sheet_every
                tile = small.copy()
                if result is not None:
                    draw(tile, result[0], result[1])
                cv2.putText(tile, f"{time:5.1f} s", (20, 60), cv2.FONT_HERSHEY_SIMPLEX, 1.8, (255, 255, 255), 5, cv2.LINE_AA)
                tiles.append(cv2.resize(tile, (480, round(sh * 480 / sw))))
            index += 1
            if index % 300 == 0:
                print(f"{index}/{total} frames", flush=True)
    video.release()

    columns = 6
    while len(tiles) % columns:
        tiles.append(np.zeros_like(tiles[0]))
    rows = [np.hstack(tiles[i:i + columns]) for i in range(0, len(tiles), columns)]
    cv2.imwrite(os.path.join(args.out, name + "-sheet.jpg"), np.vstack(rows), [cv2.IMWRITE_JPEG_QUALITY, 85])
    print(f"frames {index}, pose found {found_count} ({100 * found_count / max(1, index):.1f}%), "
          f"tracked {tracked_count}, fps {fps:.2f}, out {args.out}")


if __name__ == "__main__":
    main()
