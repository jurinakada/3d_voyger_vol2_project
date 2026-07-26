#!/usr/bin/env python3
"""Reformat the NASA AROW OEM CSV into the project's Unity playback format.

Phase 2.5/3 "real-data mode": let the existing Artemis.OrbitPlayer /
TrajectoryLoader play the real NASA Orion ephemeris with NO C# changes, by
emitting the same column layout the loader already expects:

    t_sec,phase,orion_x,orion_y,orion_z,moon_x,moon_y,moon_z,orion_vx,orion_vy,orion_vz

Inputs:
- data/processed/artemis2_trajectory.csv        (NASA AROW OEM, EME2000)
- data/processed/moon_ephemeris_horizons.csv    (JPL Horizons Moon, same frame;
  from scripts/fetch_moon_ephemeris.py). If present, Moon columns are filled by
  linear interpolation at each Orion timestamp and phases are relabeled using
  the actual Moon distance. If absent, Moon columns fall back to 0.
"""
from __future__ import annotations

import argparse
import csv
import math
from bisect import bisect_left
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_IN = ROOT / "data" / "processed" / "artemis2_trajectory.csv"
DEFAULT_MOON = ROOT / "data" / "processed" / "moon_ephemeris_horizons.csv"
DEFAULT_OUT = ROOT / "venture2_project" / "Assets" / "Artemis" / "nasa_orion_trajectory.csv"

MOON_RADIUS_KM = 1737.4
OUT_FIELDS = [
    "t_sec", "phase",
    "orion_x", "orion_y", "orion_z",
    "moon_x", "moon_y", "moon_z",
    "orion_vx", "orion_vy", "orion_vz",
]


def load_moon(path: Path):
    if not path.exists():
        return None
    ts, xs, ys, zs = [], [], [], []
    with path.open("r", encoding="utf-8", newline="") as f:
        for row in csv.DictReader(f):
            ts.append(float(row["t_sec"]))
            xs.append(float(row["x_km"]))
            ys.append(float(row["y_km"]))
            zs.append(float(row["z_km"]))
    return ts, xs, ys, zs


def moon_at(moon, t: float):
    ts, xs, ys, zs = moon
    if t <= ts[0]:
        return xs[0], ys[0], zs[0]
    if t >= ts[-1]:
        return xs[-1], ys[-1], zs[-1]
    i = bisect_left(ts, t)
    a, b = i - 1, i
    f = (t - ts[a]) / (ts[b] - ts[a])
    return (xs[a] + (xs[b] - xs[a]) * f,
            ys[a] + (ys[b] - ys[a]) * f,
            zs[a] + (zs[b] - zs[a]) * f)


def convert(in_path: Path, moon_path: Path, out_path: Path) -> None:
    moon = load_moon(moon_path)

    rows = []
    with in_path.open("r", encoding="utf-8", newline="") as src:
        for row in csv.DictReader(src):
            t = float(row["time_sec"])
            ox, oy, oz = (float(row[k]) for k in
                          ("orion_x_km", "orion_y_km", "orion_z_km"))
            if moon:
                mx, my, mz = moon_at(moon, t)
            else:
                mx = my = mz = 0.0
            rows.append({
                "t": t, "ox": ox, "oy": oy, "oz": oz,
                "mx": mx, "my": my, "mz": mz,
                "vx": row["orion_vx_km_s"], "vy": row["orion_vy_km_s"],
                "vz": row["orion_vz_km_s"],
                "earth_d": math.dist((0, 0, 0), (ox, oy, oz)),
                "moon_d": math.dist((mx, my, mz), (ox, oy, oz)) if moon else float("nan"),
                "phase_src": row["phase"],
            })

    # Phase labels. With real Moon data, label by actual geometry:
    # closest approach splits outbound coast from return.
    if moon:
        i_ca = min(range(len(rows)), key=lambda i: rows[i]["moon_d"])
        for i, r in enumerate(rows):
            if r["t"] < 86400 and r["earth_d"] < 100_000:
                r["phase"] = "EarthOrbit"
            elif r["moon_d"] < 70_000:
                r["phase"] = "LunarFlyby"
            elif i < i_ca:
                r["phase"] = "TransLunarCoast"
            else:
                r["phase"] = "EarthReturn"
        ca = rows[i_ca]
        print(f"Moon closest approach: t={ca['t']:.0f}s "
              f"({ca['t']/86400:.2f} days), center dist {ca['moon_d']:.0f} km, "
              f"altitude {ca['moon_d'] - MOON_RADIUS_KM:.0f} km")
    else:
        for r in rows:
            r["phase"] = r["phase_src"]
        print("WARNING: no Moon ephemeris found; moon columns are 0. "
              "Run scripts/fetch_moon_ephemeris.py first.")

    out_path.parent.mkdir(parents=True, exist_ok=True)
    with out_path.open("w", encoding="utf-8", newline="") as dst:
        writer = csv.DictWriter(dst, fieldnames=OUT_FIELDS)
        writer.writeheader()
        for r in rows:
            writer.writerow({
                "t_sec": f"{r['t']:.3f}", "phase": r["phase"],
                "orion_x": f"{r['ox']:.9f}", "orion_y": f"{r['oy']:.9f}",
                "orion_z": f"{r['oz']:.9f}",
                "moon_x": f"{r['mx']:.3f}", "moon_y": f"{r['my']:.3f}",
                "moon_z": f"{r['mz']:.3f}",
                "orion_vx": r["vx"], "orion_vy": r["vy"], "orion_vz": r["vz"],
            })

    print(f"Wrote {len(rows)} rows to {out_path}")
    print(f"Earth distance range: {min(r['earth_d'] for r in rows):.0f} .. "
          f"{max(r['earth_d'] for r in rows):.0f} km")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, default=DEFAULT_IN)
    parser.add_argument("--moon", type=Path, default=DEFAULT_MOON)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUT)
    args = parser.parse_args()
    convert(args.input, args.moon, args.output)


if __name__ == "__main__":
    main()
