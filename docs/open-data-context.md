# Artemis II Open Data Context

This document records the public data sources that should drive the project.
The existing Unity prototype and local CSV files are useful references, but the
project should be rebuilt around these open sources.

## Primary Source: NASA AROW Ephemeris

Source:

- NASA page: https://www.nasa.gov/missions/artemis/artemis-2/track-nasas-artemis-ii-mission-in-real-time/
- Page title: Track NASA's Artemis II Mission in Real Time
- Page date: March 6, 2026
- Page last updated: May 1, 2026
- Download section: Artemis II Ephemeris
- Download type: ZIP
- Download size shown by NASA: 1.51 MB

NASA states that AROW provides Orion state vectors and trajectory ephemeris for
use in physics models, animations, visualizations, tracking applications, and
other public projects.

The downloaded ZIP currently contains these nested files:

```text
2026.04.10 - Post-RTC3 to EI.zip
Artemis_II_OEM_2026_04_02_to_EI_v3.zip
Artemis_II_OEM_2026_04_03_to_EI.zip
Artemis_II_OEM_2026_04_04_to_EI.zip
Artemis_II_OEM_2026_04_06_Pre-OTC3_to_EI.zip
Artemis_II_OEM_2026_04_07_Pre-Lunar-Flyby_to_EI.zip
Artemis_II_OEM_2026_04_08_Post-ICPS-Sep_to_EI.zip
Artemis_II_OEM_2026_04_09_Post-ICPS-Sep_to_EI.zip
Artemis_II_OEM_2026_04_10_Post-ICPS-Sep-to-EI.zip
OEM - 2026.04.02 - post-USS-2 to EI.zip
```

## Best First OEM File

Use this file first:

```text
Artemis_II_OEM_2026_04_10_Post-ICPS-Sep-to-EI.asc
```

Reason:

- It is a CCSDS OEM 2.0 text file.
- It is Earth-centered and uses the EME2000 reference frame.
- It includes Orion position and velocity state vectors.
- It covers the mission segment from post ICPS separation to Earth entry
  interface.

Header observed in the file:

```text
CCSDS_OEM_VERS = 2.0
CREATION_DATE = 2026-04-10T03:22:19
ORIGINATOR = NASA/JSC/FOD/FDO
OBJECT_NAME = EM2
OBJECT_ID = 24
CENTER_NAME = EARTH
REF_FRAME = EME2000
TIME_SYSTEM = UTC
START_TIME = 2026-04-02T01:57:37.084
STOP_TIME = 2026-04-10T23:53:16.723
```

Data rows use this convention:

```text
UTC_ISO x_km y_km z_km vx_km_s vy_km_s vz_km_s
```

Observed stats for the current file:

- Samples: 3,262
- Start: 2026-04-02T01:57:37.084Z
- End: 2026-04-10T23:53:16.723Z
- Duration: about 8.91 days
- Earth-centered minimum distance: about 6,515 km at entry interface
- Earth-centered maximum distance: about 413,144 km
- Maximum-distance timestamp: 2026-04-06T23:02:51.667Z
- Speed range: about 0.414 km/s to 10.985 km/s

## Visualization Reference: NASA SVS

Source:

- NASA SVS page: https://svs.gsfc.nasa.gov/5632
- Title: Artemis II mission trajectory
- Released: April 6, 2026

NASA SVS describes its Artemis II mission trajectory visualization as being
based on flight-derived ephemeris data. This is a useful visual reference for
the expected route:

- Earth orbit
- outbound leg toward the Moon
- lunar flyby
- free-return style path back to Earth
- Earth entry

Use SVS as a visual target, not as the raw numeric data source.

## Supporting Research Sources

SPICE / ephemeris tooling:

- NAIF SPICE Toolkit: https://naif.jpl.nasa.gov/naif/toolkit.html
- SpiceyPy documentation: https://spiceypy.readthedocs.io/en/main/
- JPL Horizons documentation: https://ssd-api.jpl.nasa.gov/doc/horizons.html

Trajectory-control research context:

- NASA NTRS paper: https://ntrs.nasa.gov/api/citations/20250000050/downloads/Artemis2TrajOpt.pdf

The NTRS paper is useful for framing correction burns, navigation uncertainty,
and GN&C performance sensitivity. It should not be treated as a source for
reconstructing private onboard guidance state.

## Project Data Policy

Use this priority order:

1. NASA AROW OEM data for Orion's real public trajectory.
2. SPICE or JPL Horizons for Moon/Earth/Sun positions in matching frames.
3. Local simplified Python simulation for comparison and explanation.
4. Existing Unity CSV files only as prototype references.

Do not present the simplified simulation as a NASA-measured trajectory.
Do not present difference analysis as complete reverse engineering of Orion's
internal control logic.

