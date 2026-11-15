"""The car hit sounds: a toy "bonk" for the player's hits and a quiet plastic tap for the bots'.

The bonk is a falling hollow tone (a plastic toy's body ringing) over the attack of a Kenney wooden knock, so it
reads as a toy hit rather than the fist punch the game used before. The taps are Kenney's light plastic plates,
trimmed and quieter. Kenney Impact Sounds (CC0) is expected unpacked at the path given by --kenney.

    python Tools/sounds/bonk.py --kenney <Impact Sounds>/Audio --out Assets/Audio/Sfx [--preview <folder>]

Needs numpy, scipy and ffmpeg on the path. Writes mono 44.1 kHz ogg, loudness matched to the old hit clips.
"""
import argparse
import os
import subprocess
import tempfile

import numpy as np
from scipy.io import wavfile

RATE = 44100


def load(path):
    """Any audio file as mono float samples at RATE, through ffmpeg."""
    with tempfile.TemporaryDirectory() as folder:
        wav = os.path.join(folder, "x.wav")
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", path, "-ac", "1", "-ar", str(RATE), wav], check=True)
        rate, data = wavfile.read(wav)
    data = data.astype(np.float64)
    return data / (np.abs(data).max() or 1.0)


def save(samples, path):
    """Mono ogg, peak at -1 dB."""
    samples = samples / (np.abs(samples).max() or 1.0) * 10 ** (-1 / 20)
    with tempfile.TemporaryDirectory() as folder:
        wav = os.path.join(folder, "x.wav")
        wavfile.write(wav, RATE, (samples * 32767).astype(np.int16))
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", wav, "-c:a", "libvorbis", "-q:a", "5", path], check=True)


def tone(start, end, length, decay, harmonics):
    """A tone gliding from start to end Hz (fast at first), dying away with the decay time in seconds."""
    t = np.arange(int(length * RATE)) / RATE
    glide = end + (start - end) * np.exp(-t / (length * 0.18))
    phase = 2 * np.pi * np.cumsum(glide) / RATE
    body = sum(weight * np.sin(phase * ratio) for ratio, weight in harmonics)
    attack = np.minimum(1.0, t / 0.002)
    return body * attack * np.exp(-t / decay)


def bonk(knock, pitch, seed):
    """One bonk: the hollow falling tone, a soft low thump and the knock's first hit on top."""
    random = np.random.default_rng(seed)
    length = 0.34
    body = tone(520 * pitch, 190 * pitch, length, 0.085, [(1.0, 1.0), (2.01, 0.35), (2.98, 0.12)])
    thump = tone(150 * pitch, 70 * pitch, length, 0.05, [(1.0, 1.0)])
    attack = knock[: int(0.06 * RATE)] * np.exp(-np.arange(int(0.06 * RATE)) / RATE / 0.018)
    out = 0.75 * body + 0.55 * thump
    out[: attack.size] += 0.7 * attack
    out += random.normal(0, 0.004, out.size) * np.exp(-np.arange(out.size) / RATE / 0.01)
    fade = np.linspace(1, 0, int(0.03 * RATE))
    out[-fade.size:] *= fade
    return out


def tap(plate):
    """A plate's first 0.2 s, faded out: a short plastic click."""
    out = plate[: int(0.2 * RATE)].copy()
    out *= np.exp(-np.arange(out.size) / RATE / 0.06)
    return out


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--kenney", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--preview")
    args = parser.parse_args()

    knocks = [load(os.path.join(args.kenney, f"impactWood_light_00{i}.ogg")) for i in range(3)]
    for i, (knock, pitch) in enumerate(zip(knocks, [1.0, 1.06, 0.94])):
        save(bonk(knock, pitch, i), os.path.join(args.out, f"sfx_car_bonk_0{i + 1}.ogg"))

    for i in range(3):
        plate = load(os.path.join(args.kenney, f"impactPlate_light_00{i}.ogg"))
        save(tap(plate), os.path.join(args.out, f"sfx_car_tap_0{i + 1}.ogg"))

    if args.preview:
        # Other candidates to listen to: Kenney's soft thump under a wooden knock, and a plastic plate.
        os.makedirs(args.preview, exist_ok=True)
        soft = load(os.path.join(args.kenney, "impactSoft_heavy_000.ogg"))
        wood = load(os.path.join(args.kenney, "impactWood_medium_000.ogg"))
        size = max(soft.size, wood.size)
        layered = np.pad(soft, (0, size - soft.size)) * 0.8 + np.pad(wood, (0, size - wood.size)) * 0.7
        save(layered, os.path.join(args.preview, "candidate_b_soft_wood.ogg"))
        save(load(os.path.join(args.kenney, "impactPlate_medium_000.ogg")),
             os.path.join(args.preview, "candidate_c_plastic_plate.ogg"))


if __name__ == "__main__":
    main()
