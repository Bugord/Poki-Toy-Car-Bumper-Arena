"""FOOTBALL's pitch: grass. A rounded rectangle, x within +-11 and z within +-17, corners of radius 4, a goal pushed into
the middle of each short side (the blue one near the camera at -z, the red one far at +z). The numbers must match
GameTuning.FootballSettings (Assets/Scripts/Gameplay/Modes/GameTuningFootball.cs)."""
import numpy as np
from floorlib import *

HX, HZ, R = 11.0, 17.0, 4.0
GOAL_HALF, GOAL_DEPTH = 3.5, 2.2
LINE_Z = HZ - GOAL_DEPTH          # the goal line, across the mouth
WHITE = col(250, 252, 246)
BLUE = col(64, 140, 255)
RED = col(255, 92, 92)


def outline():
    """The rim, counter-clockwise from the bottom middle, as ArenaShape builds a rounded square."""
    pts = [(0.0, -HZ)]
    for cx, cz, a0 in ((HX - R, -(HZ - R), -0.5), (HX - R, HZ - R, 0.0), (-(HX - R), HZ - R, 0.5), (-(HX - R), -(HZ - R), 1.0)):
        for i in range(9):
            a = np.pi * (a0 + 0.5 * i / 8)
            pts.append((round(cx + R * np.cos(a), 3), round(cz + R * np.sin(a), 3)))
    return pts


def line(d, mpp, width=0.14):
    return sm(width - d, mpp)


def pitch_top(fp, f):
    X, Z, d = f['X'], f['Z'], f['d']
    sh = X.shape
    m = self_mpp(f)
    # mown stripes across the length, one shade then the other
    band = np.floor((Z + HZ) / 2.6)
    light = (band % 2 == 0)
    base = np.where(light[..., None], col(120, 206, 92), col(96, 186, 74))
    blades = noise(sh, (40, 40), 11, 3)
    base = base * (1 + 0.05 * blades)[..., None]
    clumps = np.clip(noise(sh, (6, 6), 12, 2) - 0.5, 0, 1)
    base = base * (1 - 0.12 * clumps)[..., None]
    # behind each goal line, outside the goal, the grass is a touch darker: the run-off
    behind = sm(np.abs(Z) - LINE_Z, m)
    base = mix(base, base * 0.88, behind)
    # inside each goal the net's shade and the team's colour
    in_goal = sm(np.abs(Z) - LINE_Z, m) * sm(GOAL_HALF - np.abs(X), m)
    team = np.where((Z > 0)[..., None], RED, BLUE)
    net = mix(team, WHITE, 0.35)
    net = net * (1 - 0.06 * blades)[..., None]
    base = mix(base, net, in_goal * 0.9)
    # white markings
    marks = np.zeros(sh, np.float32)
    marks = np.maximum(marks, line(np.abs(Z), m))                                   # halfway line
    r = np.hypot(X, Z)
    marks = np.maximum(marks, line(np.abs(r - 3.6), m))                             # centre circle
    marks = np.maximum(marks, sm(0.32 - r, m))                                      # centre spot
    marks = np.maximum(marks, line(np.abs(d - 0.75), m))                            # touchline
    # the goal lines, across the whole pitch at the mouth
    marks = np.maximum(marks, line(np.abs(np.abs(Z) - LINE_Z), m) * sm(d - 0.6, m))
    # the penalty box and the goal area at each end
    for half_w, depth in ((6.6, 5.4), (4.6, 2.2)):
        inside_x = np.abs(X) <= half_w
        front = LINE_Z - depth
        box_front = line(np.abs(np.abs(Z) - front), m) * sm(half_w + 0.07 - np.abs(X), m)
        box_side = line(np.abs(np.abs(X) - half_w), m) * sm(np.abs(Z) - front + 0.07, m) * sm(LINE_Z + 0.07 - np.abs(Z), m)
        marks = np.maximum(marks, np.maximum(box_front, box_side))
    # penalty spot and the arc outside the box
    pz = LINE_Z - 4.0
    pr = np.hypot(X, np.abs(Z) - pz)
    marks = np.maximum(marks, sm(0.22 - pr, m))
    arc = line(np.abs(pr - 2.6), m) * sm(LINE_Z - 5.4 - np.abs(Z), m)
    marks = np.maximum(marks, arc)
    base = mix(base, WHITE, marks * 0.95)
    return np.clip(base, 0, 1)


def pitch_side(XM, YM):
    base = np.zeros(XM.shape + (3,), np.float32) + col(110, 74, 46)
    grass = sm(0.09 - YM, 0.01)
    base = mix(base, col(96, 186, 74), grass)
    base = base * (1 - 0.06 * np.sin(XM * 9.0 + 3.0 * np.sin(YM * 40.0)))[..., None]
    return base


def register(d):
    d['pitch_grass'] = ('pitch', pitch_top, pitch_side)
