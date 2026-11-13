"""Check pictures for a converted senam: draws the targets from the choreography file back onto the
instructor in the original video. Called by Pose-To-Senam.py with --video.

Writes <base>-targets.jpg (one tile per target moment: the hand, foot and head targets must sit on the
instructor) and <base>-overlay.mp4 (the whole video, cropped to the instructor, with each target fading
in 0.8 s before its moment like in the game). Hand targets are yellow, foot targets blue, head targets
purple; the circle is the catch radius used by the game.
"""
import cv2
import numpy as np

LEAD = .8          # seconds a target is shown before its moment (as in the game)
RADIUS = .45       # catch radius in torso lengths (as in the game)
HAND, FOOT, HEAD, HIPS = (0, 210, 255), (255, 170, 40), (200, 60, 200), (40, 140, 255)


def parse(path):
    events = []
    for line in open(path, encoding="utf-8"):
        f = line.split()
        if not f or f[0] in ("#", "g"):
            continue
        k = f[0]
        if k == "hands":
            t, hold = float(f[1]), float(f[2])
            events.append({"kind": k, "t": t, "end": t + hold, "points": [(float(f[3]), float(f[4]), HAND), (float(f[6]), float(f[7]), HAND)]})
        elif k == "foot":
            t, hold = float(f[1]), float(f[2])
            events.append({"kind": k, "t": t, "end": t + hold, "points": [(float(f[4]), float(f[5]), FOOT)], "label": f[6]})
        elif k == "head":
            events.append({"kind": k, "t": float(f[1]), "end": float(f[1]), "tilt": float(f[2]), "turn": float(f[3])})
        elif k == "hips":
            events.append({"kind": k, "t": float(f[1]), "end": float(f[2]), "points": [(float(f[3]), float(f[4]), HIPS), (float(f[5]), float(f[6]), HIPS)]})
        elif k == "march":
            events.append({"kind": k, "t": float(f[1]), "end": float(f[2]), "steps": int(f[3])})
    return events


def h264(raw, original, out):
    """Re-encode to H.264 with the original music (plays everywhere) when imageio-ffmpeg is installed."""
    import os
    import subprocess
    try:
        import imageio_ffmpeg
        ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
    except ImportError:
        os.replace(raw, out)
        return
    result = subprocess.run([ffmpeg, "-y", "-loglevel", "error", "-i", raw, "-i", original, "-map", "0:v", "-map", "1:a?",
                             "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "24", "-c:a", "aac", "-shortest", out])
    if result.returncode == 0:
        os.remove(raw)
    else:
        os.replace(raw, out)


def run(video_path, choreography, senam, base, aspect=16 / 9):
    events = parse(choreography)
    t = senam.t
    # Crop around the instructor over the whole video.
    xs = senam.iso[:, :, 0] / aspect
    ys = 1 - senam.iso[:, :, 1]
    x0, x1 = max(0, np.percentile(xs, 1) - .06), min(1, np.percentile(xs, 99) + .06)
    y0, y1 = max(0, np.percentile(ys, 1) - .1), min(1, np.percentile(ys, 99) + .04)

    def to_pixel(i, point, w, h):
        iso = senam.ref[i] + np.array(point) * senam.torso
        return int((iso[0] / aspect - x0) / (x1 - x0) * w), int(((1 - iso[1]) - y0) / (y1 - y0) * h)

    video = cv2.VideoCapture(video_path)
    fps = video.get(cv2.CAP_PROP_FPS) or 30
    out_w = 540
    writer, tiles = None, []
    tile_times = sorted({round(e["t"], 3) for e in events if e["kind"] in ("hands", "foot", "head", "hips")})
    next_tile = 0
    index = 0
    while True:
        ok, frame = video.read()
        if not ok:
            break
        time = index / fps
        H, W = frame.shape[:2]
        crop = frame[int(y0 * H):int(y1 * H), int(x0 * W):int(x1 * W)]
        out_h = round(crop.shape[0] * out_w / crop.shape[1])
        crop = cv2.resize(crop, (out_w, out_h), interpolation=cv2.INTER_AREA)
        if writer is None:
            writer = cv2.VideoWriter(base + "-overlay-raw.mp4", cv2.VideoWriter_fourcc(*"mp4v"), fps, (out_w, out_h))
        i = min(index, len(t) - 1)
        radius = int(RADIUS * senam.torso / (y1 - y0) * out_h)
        labels = []
        for e in events:
            if e["kind"] == "march":
                if e["t"] <= time <= e["end"]:
                    done = int(e["steps"] * (time - e["t"]) / max(.1, e["end"] - e["t"]))
                    labels.append(f"Jalan di tempat {done}/{e['steps']}")
                continue
            if not (e["t"] - LEAD <= time <= e["end"] + .3):
                continue
            k = int(round(e["t"] * fps))
            k = min(k, len(t) - 1)
            grow = min(1, (time - (e["t"] - LEAD)) / LEAD)
            thick = 3 if time >= e["t"] - .05 else 1
            if e["kind"] == "head":
                nose = to_pixel(k, ((senam.b[k, 0, 0]), (senam.b[k, 0, 1])), out_w, out_h)
                a = np.radians(90 + e["tilt"])
                tip = (int(nose[0] + np.cos(a) * radius * 1.2), int(nose[1] - np.sin(a) * radius * 1.2))
                cv2.circle(crop, nose, int(radius * .8 * grow), HEAD, thick, cv2.LINE_AA)
                cv2.line(crop, nose, tip, HEAD, 3, cv2.LINE_AA)
                labels.append(("Tekuk" if abs(e["tilt"]) / 7 >= abs(e["turn"]) / .4 else "Tengok") + " kepala")
                continue
            for x, y, color in e["points"]:
                cv2.circle(crop, to_pixel(k, (x, y), out_w, out_h), max(2, int(radius * grow)), color, thick, cv2.LINE_AA)
            if e["kind"] == "hips":
                labels.append("Tangan di pinggang")
            elif e["kind"] == "foot":
                labels.append("Angkat lutut" if e["label"] == "raise" else "Langkah kaki")
        for n, text in enumerate(dict.fromkeys(labels)):
            cv2.putText(crop, text, (10, 28 + 24 * n), cv2.FONT_HERSHEY_SIMPLEX, .65, (20, 20, 20), 3, cv2.LINE_AA)
            cv2.putText(crop, text, (10, 28 + 24 * n), cv2.FONT_HERSHEY_SIMPLEX, .65, (255, 255, 255), 1, cv2.LINE_AA)
        cv2.putText(crop, f"{time:6.2f} s", (out_w - 120, out_h - 12), cv2.FONT_HERSHEY_SIMPLEX, .6, (60, 60, 60), 2, cv2.LINE_AA)
        writer.write(crop)
        # Contact sheet: the frame at each target moment.
        while next_tile < len(tile_times) and time >= tile_times[next_tile] - .5 / fps:
            tile = cv2.resize(crop, (180, round(out_h * 180 / out_w)))
            tiles.append(tile)
            next_tile += 1
        index += 1
    video.release()
    if writer is not None:
        writer.release()
        h264(base + "-overlay-raw.mp4", video_path, base + "-overlay.mp4")
    if tiles:
        columns = 12
        while len(tiles) % columns:
            tiles.append(np.full_like(tiles[0], 255))
        sheet = np.vstack([np.hstack(tiles[r:r + columns]) for r in range(0, len(tiles), columns)])
        cv2.imwrite(base + "-targets.jpg", sheet, [cv2.IMWRITE_JPEG_QUALITY, 80])
    print(f"check pictures: {base}-targets.jpg ({len(tiles)} moments), {base}-overlay.mp4")
