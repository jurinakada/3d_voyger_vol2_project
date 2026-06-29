# Data

This directory is for generated research data.

- `processed/artemis2_trajectory.csv` is generated from NASA AROW CCSDS OEM data.
- `processed/artemis2_trajectory_manifest.json` records the exact source and summary statistics.

Regenerate from a previously downloaded NASA ZIP:

```bash
python3 scripts/build_artemis2_dataset.py --source /tmp/all-artemis-ii-oem-files-current.zip
```

Regenerate by downloading from NASA:

```bash
python3 scripts/build_artemis2_dataset.py
```

