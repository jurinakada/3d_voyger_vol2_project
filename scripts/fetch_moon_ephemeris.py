#!/usr/bin/env python3
"""Fetch geocentric Moon ephemeris from JPL Horizons for the Artemis II window.

Frame: ICRF/J2000 equatorial, Earth-centered ("500@399") — this matches the
NASA OEM's EME2000 to within milliarcseconds, so Moon and Orion live in the
same frame with no rotation needed.

Time note: Horizons vector epochs are TDB. TDB-UTC is ~69.2 s in 2026; the
Moon moves ~1 km/s, so ignoring the offset would shift it ~70 km (0.02% of
its distance). We subtract the offset anyway to keep timestamps honest.

Output: data/processed/moon_ephemeris_horizons.csv
    t_sec,x_km,y_km,z_km,vx_km_s,vy_km_s,vz_km_s
where t_sec is seconds since the OEM start epoch 2026-04-02T01:57:37.084Z.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timedelta, timezone
from pathlib import Path
from urllib.parse import urlencode
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUT = ROOT / "data" / "processed" / "moon_ephemeris_horizons.csv"
DEFAULT_RAW = ROOT / "data" / "processed" / "moon_ephemeris_horizons_raw.txt"

OEM_START_UTC = datetime(2026, 4, 2, 1, 57, 37, 84000, tzinfo=timezone.utc)
TDB_MINUS_UTC_SEC = 69.184  # TT-UTC (32.184 + 37 leap seconds); TDB≈TT here.

API = "https://ssd.jpl.nasa.gov/api/horizons.api"
PARAMS = {
    "format": "text",
    "COMMAND": "'301'",            # Moon
    "OBJ_DATA": "'NO'",
    "MAKE_EPHEM": "'YES'",
    "EPHEM_TYPE": "'VECTORS'",
    "CENTER": "'500@399'",         # geocentric
    "REF_PLANE": "'FRAME'",        # ICRF/J2000 equatorial (= EME2000)
    "REF_SYSTEM": "'ICRF'",
    "VEC_TABLE": "'2'",            # position + velocity
    "OUT_UNITS": "'KM-S'",
    "CSV_FORMAT": "'YES'",
    "START_TIME": "'2026-04-02 01:00'",
    "STOP_TIME": "'2026-04-11 00:30'",
    "STEP_SIZE": "'10m'",
}

MONTHS = {m: i + 1 for i, m in enumerate(
    ["Jan", "Feb", "Mar", "Apr", "May", "Jun",
     "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"])}


def parse_tdb(cal: str) -> datetime:
    # e.g. "A.D. 2026-Apr-02 01:00:00.0000"
    cal = cal.replace("A.D.", "").strip()
    date_part, time_part = cal.split()
    y, mon, d = date_part.split("-")
    hh, mm, ss = time_part.split(":")
    sec = float(ss)
    return datetime(int(y), MONTHS[mon], int(d), int(hh), int(mm),
                    tzinfo=timezone.utc) + timedelta(seconds=sec)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUT)
    parser.add_argument("--raw", type=Path, default=DEFAULT_RAW)
    args = parser.parse_args()

    url = API + "?" + urlencode(PARAMS, safe="'@ ")
    print(f"GET {url}")
    with urlopen(url, timeout=60) as resp:
        text = resp.read().decode("utf-8", errors="replace")

    args.raw.parent.mkdir(parents=True, exist_ok=True)
    args.raw.write_text(text, encoding="utf-8")

    if "$$SOE" not in text:
        raise SystemExit("Horizons response has no $$SOE data block; see raw file")
    block = text.split("$$SOE")[1].split("$$EOE")[0]

    rows = []
    for line in block.strip().splitlines():
        parts = [p.strip() for p in line.split(",")]
        if len(parts) < 8:
            continue
        tdb = parse_tdb(parts[1])
        utc = tdb - timedelta(seconds=TDB_MINUS_UTC_SEC)
        t_sec = (utc - OEM_START_UTC).total_seconds()
        x, y, z, vx, vy, vz = (float(v) for v in parts[2:8])
        rows.append((t_sec, x, y, z, vx, vy, vz))

    with args.output.open("w", encoding="utf-8", newline="") as f:
        f.write("t_sec,x_km,y_km,z_km,vx_km_s,vy_km_s,vz_km_s\n")
        for r in rows:
            f.write(f"{r[0]:.3f}," + ",".join(f"{v:.9f}" for v in r[1:]) + "\n")

    print(f"Wrote {len(rows)} Moon states to {args.output}")
    print(f"t_sec range: {rows[0][0]:.0f} .. {rows[-1][0]:.0f}")


if __name__ == "__main__":
    main()
