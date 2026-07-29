#!/usr/bin/env python3
"""Build normalized Artemis II trajectory CSVs from NASA AROW OEM data."""

from __future__ import annotations

import argparse
import json
import shutil
import tempfile
import zipfile
from datetime import datetime, timezone
from pathlib import Path
from urllib.request import urlretrieve

from convert_nasa_oem import convert_oem, iter_oem_states


DEFAULT_NASA_OEM_ZIP_URL = (
    "https://www.nasa.gov/wp-content/uploads/2026/03/"
    "all-artemis-ii-oem-files.zip?emrc=6a1d3c14e48f4"
)

PREFERRED_OEM_ZIP = "Artemis_II_OEM_2026_04_10_Post-ICPS-Sep-to-EI.zip"


def copy_or_download_zip(source: str, target_dir: Path) -> Path:
    target_path = target_dir / "all-artemis-ii-oem-files.zip"
    if source.startswith(("http://", "https://")):
        urlretrieve(source, target_path)
        return target_path

    source_path = Path(source)
    if not source_path.exists():
        raise FileNotFoundError(f"OEM ZIP not found: {source_path}")

    shutil.copyfile(source_path, target_path)
    return target_path


def extract_preferred_oem(outer_zip_path: Path, work_dir: Path) -> Path:
    outer_dir = work_dir / "outer"
    inner_dir = work_dir / "inner"
    outer_dir.mkdir(parents=True, exist_ok=True)
    inner_dir.mkdir(parents=True, exist_ok=True)

    with zipfile.ZipFile(outer_zip_path) as outer_zip:
        names = outer_zip.namelist()
        if PREFERRED_OEM_ZIP not in names:
            raise FileNotFoundError(f"{PREFERRED_OEM_ZIP} not found in {outer_zip_path}")
        outer_zip.extract(PREFERRED_OEM_ZIP, outer_dir)

    preferred_zip_path = outer_dir / PREFERRED_OEM_ZIP
    with zipfile.ZipFile(preferred_zip_path) as inner_zip:
        inner_zip.extractall(inner_dir)

    asc_files = sorted(inner_dir.glob("*.asc"))
    if len(asc_files) != 1:
        raise ValueError(f"Expected exactly one .asc file in {PREFERRED_OEM_ZIP}, found {len(asc_files)}")

    return asc_files[0]


def summarize_oem(oem_path: Path) -> dict:
    states = list(iter_oem_states(oem_path))
    if not states:
        raise ValueError(f"No OEM states found in {oem_path}")

    start = states[0].timestamp
    end = states[-1].timestamp
    distances = [(state.x_km**2 + state.y_km**2 + state.z_km**2) ** 0.5 for state in states]
    speeds = [(state.vx_km_s**2 + state.vy_km_s**2 + state.vz_km_s**2) ** 0.5 for state in states]
    max_distance_index = max(range(len(distances)), key=distances.__getitem__)
    min_distance_index = min(range(len(distances)), key=distances.__getitem__)

    return {
        "oem_file": oem_path.name,
        "samples": len(states),
        "start_utc": start.isoformat().replace("+00:00", "Z"),
        "end_utc": end.isoformat().replace("+00:00", "Z"),
        "duration_sec": (end - start).total_seconds(),
        "duration_days": (end - start).total_seconds() / 86400,
        "center": "EARTH",
        "reference_frame": "EME2000",
        "time_system": "UTC",
        "position_unit": "km",
        "velocity_unit": "km/s",
        "min_earth_distance_km": min(distances),
        "min_earth_distance_utc": states[min_distance_index].timestamp.isoformat().replace("+00:00", "Z"),
        "max_earth_distance_km": max(distances),
        "max_earth_distance_utc": states[max_distance_index].timestamp.isoformat().replace("+00:00", "Z"),
        "min_speed_km_s": min(speeds),
        "max_speed_km_s": max(speeds),
    }


def write_manifest(path: Path, source: str, selected_oem_zip: str, summary: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    manifest = {
        "generated_at_utc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "source": source,
        "source_page": "https://www.nasa.gov/missions/artemis/artemis-2/track-nasas-artemis-ii-mission-in-real-time/",
        "selected_oem_zip": selected_oem_zip,
        "summary": summary,
        "notes": [
            "NASA AROW OEM is treated as the public Orion trajectory baseline.",
            "Moon columns are intentionally blank until SPICE or Horizons alignment is added.",
            "Unity should convert km to scene units during import/playback.",
        ],
    }
    path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--source",
        default=DEFAULT_NASA_OEM_ZIP_URL,
        help="NASA AROW OEM ZIP URL or a local all-artemis-ii-oem-files.zip path",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("data/processed/artemis2_trajectory.csv"),
        help="Normalized CSV output path",
    )
    parser.add_argument(
        "--unity-output",
        type=Path,
        default=Path("venture2_project/Assets/Resources/CSV/artemis2_trajectory.csv"),
        help="Optional Unity Resources CSV output path",
    )
    parser.add_argument(
        "--manifest",
        type=Path,
        default=Path("data/processed/artemis2_trajectory_manifest.json"),
        help="Manifest JSON output path",
    )
    args = parser.parse_args()

    with tempfile.TemporaryDirectory(prefix="artemis2_oem_") as temp_name:
        work_dir = Path(temp_name)
        outer_zip_path = copy_or_download_zip(args.source, work_dir)
        oem_path = extract_preferred_oem(outer_zip_path, work_dir)

        row_count = convert_oem(oem_path, args.output)
        args.unity_output.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(args.output, args.unity_output)

        summary = summarize_oem(oem_path)
        write_manifest(args.manifest, args.source, PREFERRED_OEM_ZIP, summary)

    print(f"Wrote {row_count} rows to {args.output}")
    print(f"Copied Unity CSV to {args.unity_output}")
    print(f"Wrote manifest to {args.manifest}")


if __name__ == "__main__":
    main()

