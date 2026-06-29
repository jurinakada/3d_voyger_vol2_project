#!/usr/bin/env python3
"""Convert NASA CCSDS OEM trajectory files into Unity-friendly CSV.

Input rows are expected to follow the CCSDS OEM convention:

    UTC_ISO x_km y_km z_km vx_km_s vy_km_s vz_km_s

Metadata lines and comments are ignored. The output intentionally uses the
planned Unity column names for Artemis II work.
"""

from __future__ import annotations

import argparse
import csv
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path


UNITY_FIELDS = [
    "time_sec",
    "utc",
    "orion_x_km",
    "orion_y_km",
    "orion_z_km",
    "orion_vx_km_s",
    "orion_vy_km_s",
    "orion_vz_km_s",
    "moon_x_km",
    "moon_y_km",
    "moon_z_km",
    "phase",
    "event",
    "source",
]


@dataclass(frozen=True)
class OemState:
    timestamp: datetime
    x_km: float
    y_km: float
    z_km: float
    vx_km_s: float
    vy_km_s: float
    vz_km_s: float


def parse_utc(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00")).replace(tzinfo=timezone.utc)


def iter_oem_states(path: Path):
    with path.open("r", encoding="utf-8", errors="replace") as source:
        for line in source:
            stripped = line.strip()
            if not stripped or stripped.startswith("COMMENT"):
                continue

            parts = stripped.split()
            if len(parts) != 7 or not parts[0][0].isdigit():
                continue

            try:
                yield OemState(
                    timestamp=parse_utc(parts[0]),
                    x_km=float(parts[1]),
                    y_km=float(parts[2]),
                    z_km=float(parts[3]),
                    vx_km_s=float(parts[4]),
                    vy_km_s=float(parts[5]),
                    vz_km_s=float(parts[6]),
                )
            except ValueError:
                continue


def classify_phase(time_sec: float, earth_distance_km: float) -> str:
    """Coarse labels for early visualization until event reconstruction exists."""
    if earth_distance_km < 80_000 and time_sec < 24 * 3600:
        return "EarthOrbit"
    if earth_distance_km < 250_000:
        return "TransLunarCoast"
    if earth_distance_km > 250_000 and time_sec < 6 * 24 * 3600:
        return "LunarFlyby"
    return "EarthReturn"


def convert_oem(input_path: Path, output_path: Path) -> int:
    states = list(iter_oem_states(input_path))
    if not states:
        raise ValueError(f"No OEM state vectors found in {input_path}")

    start = states[0].timestamp
    output_path.parent.mkdir(parents=True, exist_ok=True)

    with output_path.open("w", newline="", encoding="utf-8") as target:
        writer = csv.DictWriter(target, fieldnames=UNITY_FIELDS)
        writer.writeheader()

        for index, state in enumerate(states):
            time_sec = (state.timestamp - start).total_seconds()
            earth_distance_km = (state.x_km**2 + state.y_km**2 + state.z_km**2) ** 0.5
            event = "Start" if index == 0 else "None"

            writer.writerow(
                {
                    "time_sec": f"{time_sec:.3f}",
                    "utc": state.timestamp.isoformat().replace("+00:00", "Z"),
                    "orion_x_km": f"{state.x_km:.9f}",
                    "orion_y_km": f"{state.y_km:.9f}",
                    "orion_z_km": f"{state.z_km:.9f}",
                    "orion_vx_km_s": f"{state.vx_km_s:.12f}",
                    "orion_vy_km_s": f"{state.vy_km_s:.12f}",
                    "orion_vz_km_s": f"{state.vz_km_s:.12f}",
                    "moon_x_km": "",
                    "moon_y_km": "",
                    "moon_z_km": "",
                    "phase": classify_phase(time_sec, earth_distance_km),
                    "event": event,
                    "source": "NASA_AROW_OEM",
                }
            )

    return len(states)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input_oem", type=Path, help="NASA CCSDS OEM .asc file")
    parser.add_argument("output_csv", type=Path, help="Unity-friendly output CSV")
    args = parser.parse_args()

    count = convert_oem(args.input_oem, args.output_csv)
    print(f"Converted {count} OEM state vectors to {args.output_csv}")


if __name__ == "__main__":
    main()
