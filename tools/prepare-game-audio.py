"""Trim and loudness-match game cues; requires numpy, scipy, soundfile, pyloudnorm."""
import argparse
import json
from pathlib import Path

import numpy as np
import pyloudnorm
import soundfile
from scipy.signal import resample_poly


def loudness(samples, rate):
    # BS.1770 uses 400 ms blocks. Pad short transients for the same measurement window.
    padded = np.pad(samples, ((0, max(0, int(rate * 0.4) + 1 - len(samples))), (0, 0)))
    return pyloudnorm.Meter(rate).integrated_loudness(padded)


def true_peak(samples):
    peak = np.max(np.abs(resample_poly(samples, 4, 1, axis=0)))
    return float(20 * np.log10(max(peak, 1e-12)))


def prepare(path):
    samples, rate = soundfile.read(path, always_2d=True, dtype="float64")
    if samples.shape[1] > 2 or not np.isfinite(samples).all():
        raise ValueError(f"Invalid audio channels/samples: {path}")
    peak = np.max(np.abs(samples))
    if peak <= 1e-6:
        raise ValueError(f"Silent cue: {path}")
    threshold = max(1e-5, peak * 10 ** (-40 / 20))
    audible = np.flatnonzero(np.max(np.abs(samples), axis=1) >= threshold)
    start = max(0, int(audible[0]) - int(rate * 0.001))
    end = min(len(samples), int(audible[-1]) + int(rate * 0.02) + 1)
    trimmed = samples[start:end].copy()
    # Tiny edge fades prevent discontinuities without softening the original attack.
    fade_in = min(len(trimmed), max(1, int(rate * 0.0002)))
    fade_out = min(len(trimmed), max(1, int(rate * 0.001)))
    trimmed[:fade_in] *= np.linspace(0, 1, fade_in)[:, None]
    trimmed[-fade_out:] *= np.linspace(1, 0, fade_out)[:, None]
    measured = float(loudness(trimmed, rate))
    if not np.isfinite(measured):
        raise ValueError(f"Cannot measure loudness: {path}")
    return trimmed, rate, measured, true_peak(trimmed), start * 1000 / rate


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=Path("Assets/Sounds/Source"))
    parser.add_argument("--output", type=Path, default=Path("Assets/Sounds/Resources/Audio"))
    parser.add_argument("--report", type=Path, default=Path(".utmp/game-audio-levels.json"))
    args = parser.parse_args()
    cues = {name: prepare(args.source / f"{name}.mp3") for name in ("ButtonClick", "Point", "Ready")}
    # Reduce the common target if any cue needs more headroom; never distort transients with a limiter.
    target = min(-20.0, *(value[2] - 3.0 - value[3] for value in cues.values()))
    report = {"target_lufs": target, "true_peak_ceiling_db": -3.0, "clips": {}}
    args.output.mkdir(parents=True, exist_ok=True)
    for name, (samples, rate, measured, peak, removed_ms) in cues.items():
        path = args.output / f"{name}.wav"
        soundfile.write(path, samples * 10 ** ((target - measured) / 20), rate, subtype="PCM_16")
        output, _ = soundfile.read(path, always_2d=True)
        actual_lufs = float(loudness(output, rate))
        actual_peak = true_peak(output)
        audible = np.flatnonzero(np.max(np.abs(output), axis=1) >= np.max(np.abs(output)) * 0.01)
        onset_ms = float(audible[0] * 1000 / rate)
        if abs(actual_lufs - target) > 0.1 or actual_peak > -2.95 or onset_ms > 5:
            raise ValueError(f"Prepared cue failed loudness/peak/onset validation: {name}")
        report["clips"][name] = {"removed_leading_ms": removed_ms, "onset_ms": onset_ms,
                                "lufs": actual_lufs, "true_peak_db": actual_peak,
                                "duration_seconds": len(output) / rate}
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
