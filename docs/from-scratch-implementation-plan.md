# From-Scratch Implementation Plan

This plan intentionally starts from the public Artemis II data model instead of
the current Unity scene. Existing files can be referenced, but should not define
the architecture.

## Target

Build an educational Unity / VR visualization that lets the viewer understand:

- where Orion is relative to Earth and the Moon
- when Orion leaves Earth orbit, approaches the Moon, flies by, and returns
- how a simplified three-body model differs from the public NASA ephemeris
- why Unity should replay trajectory data rather than numerically integrate the
  mission in the game loop

## Core Data Model

Use one normalized CSV schema for Unity:

```csv
time_sec,utc,orion_x_km,orion_y_km,orion_z_km,orion_vx_km_s,orion_vy_km_s,orion_vz_km_s,moon_x_km,moon_y_km,moon_z_km,phase,event,source
```

Coordinate policy:

- Keep raw data in km and UTC.
- Treat NASA OEM as Earth-centered EME2000 unless converted.
- Convert to Unity units only inside Unity import/playback code.
- Use `1 Unity unit = 10,000 km` for trajectory positions.
- Enlarge Earth, Moon, and Orion visually; do not change the raw trajectory.

## Pipeline

1. Download NASA AROW OEM ZIP.
2. Extract the selected CCSDS OEM `.asc` file.
3. Parse Orion UTC, position, and velocity.
4. Fetch or compute Moon position at the same UTC timestamps.
5. Write normalized Unity CSV.
6. Run a simplified Python simulation from comparable initial conditions.
7. Resample both trajectories onto a common timeline.
8. Compute difference vectors and summary metrics.
9. Import the real trajectory, simplified trajectory, and difference data into
   Unity.
10. Build VR/Sony Display views on top of that data.

Current implemented entry point:

```bash
python3 scripts/build_artemis2_dataset.py --source /tmp/all-artemis-ii-oem-files-current.zip
```

This writes:

- `data/processed/artemis2_trajectory.csv`
- `data/processed/artemis2_trajectory_manifest.json`
- `venture2_project/Assets/Resources/CSV/artemis2_trajectory.csv`

## Unity Modules To Build Fresh

These should replace the current one-frame-per-row loader:

- `TrajectoryCsvLoader`
  - parses the normalized CSV
  - stores timestamp, UTC, position, velocity, phase, event, source
- `TrajectoryPlayer`
  - plays trajectory by mission time
  - supports speed multiplier, pause, scrub, and loop
  - interpolates between samples
- `OrbitLineRenderer`
  - draws real trajectory and simplified trajectory separately
  - supports phase coloring
- `MoonAnimator`
  - animates Moon from the same normalized timeline
- `TrajectoryComparison`
  - displays or exports difference distance and direction
- `VRScaleController`
  - switches between full mission scale and close-up teaching scale

## First Milestone

Milestone 1 should be data-only and non-VR:

- NASA OEM parses successfully.
- A normalized CSV is generated.
- Unity can display Orion's real public path as a line.
- Orion can move along the line based on mission time.
- Earth and Moon positions use a consistent scale.

## Second Milestone

Milestone 2 adds the educational comparison:

- simplified Python trajectory generated from explicit assumptions
- real and simplified lines shown together
- difference graph or visual vectors shown at selected timestamps
- labels for Earth orbit, trans-lunar coast, lunar flyby, and Earth return

## Third Milestone

Milestone 3 adds immersive display:

- VR camera rig
- Sony Spatial Reality Display camera/view configuration
- scale controls
- event jump buttons
- stable performance with line rendering and model assets

## Existing Files: Reference Only

Current useful references:

- `scripts/orbit_sim.py`
- `output/artemis1_trajectory.csv`
- `output/moon_trajectory.csv`
- `venture2_project/Assets/Scripts/TrajectoryLoader.cs`
- `venture2_project/Assets/Scenes/MainScene.unity`

Known issue:

- `venture2_project/Assets/CSV/artemis1_trajectory.csv` does not match the
  current `scripts/orbit_sim.py` output, so it should not be treated as
  canonical.
