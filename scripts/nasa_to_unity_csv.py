#!/usr/bin/env python3
"""Reformat the NASA AROW OEM CSV into the project's Unity playback format.

Phase 2.5 "real-data mode": let the existing Artemis.OrbitPlayer / TrajectoryLoader
play the real NASA Orion ephemeris with NO C# changes, by emitting the same column
layout the loader already expects:

    t_sec,phase,orion_x,orion_y,orion_z,moon_x,moon_y,moon_z,orion_vx,orion_vy,orion_vz

Input (NASA, from scripts/convert_nasa_oem.py):
    time_sec,utc,orion_x_km,orion_y_km,orion_z_km,orion_vx_km_s,orion_vy_km_s,
    orion_vz_km_s,moon_x_km,moon_y_km,moon_z_km,phase,event,source

Notes:
- Positions/velocities are real EME2000 (Earth-centered, 3D, km / km/s) and are
  passed through unchanged.
- Moon columns are placeholders (0) here. The NASA OEM has no Moon position;
  real Moon ephemeris (Horizons/SPICE, same frame+epoch) is Phase 3 work.
"""
from __future__ import annotations

import argparse
import csv
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_IN = ROOT / "data" / "processed" / "artemis2_trajectory.csv"
DEFAULT_OUT = ROOT / "venture2_project" / "Assets" / "Artemis" / "nasa_orion_trajectory.csv"

OUT_FIELDS = [
    "t_sec", "phase",
    "orion_x", "orion_y", "orion_z",
    "moon_x", "moon_y", "moon_z",
    "orion_vx", "orion_vy", "orion_vz",
]


def convert(in_path: Path, out_path: Path) -> int:
    out_path.parent.mkdir(parents=True, exist_ok=True)
    count = 0
    with in_path.open("r", encoding="utf-8", newline="") as src, \
         out_path.open("w", encoding="utf-8", newline="") as dst:
        reader = csv.DictReader(src)
        writer = csv.DictWriter(dst, fieldnames=OUT_FIELDS)
        writer.writeheader()
        for row in reader:
            writer.writerow({
                "t_sec": row["time_sec"],
                "phase": row["phase"],
                "orion_x": row["orion_x_km"],
                "orion_y": row["orion_y_km"],
                "orion_z": row["orion_z_km"],
                "moon_x": "0", "moon_y": "0", "moon_z": "0",
                "orion_vx": row["orion_vx_km_s"],
                "orion_vy": row["orion_vy_km_s"],
                "orion_vz": row["orion_vz_km_s"],
            })
            count += 1
    return count


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, default=DEFAULT_IN)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUT)
    args = parser.parse_args()
    n = convert(args.input, args.output)
    print(f"Wrote {n} rows to {args.output}")


if __name__ == "__main__":
    main()
