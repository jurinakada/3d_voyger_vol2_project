#!/usr/bin/env python3
"""Align the NASA trajectory with the physics simulation for overlay comparison.

The physics model lives in its own frame: Earth-centered, Moon on a circular
counterclockwise orbit in the xy-plane (radius 384,400 km), epoch t=0 at
parking-orbit start. The NASA data lives in EME2000 with real epochs. To draw
both in one scene we map NASA -> physics frame:

1. Plane: rotate EME2000 so the Moon's orbital plane (from the angular
   momentum r x v of the Horizons Moon around flyby) becomes the xy-plane.
2. Azimuth: rotate about z so both Moons sit at the same angle at their
   respective lunar closest-approach (CA) moments.
3. Time: shift NASA t so both CAs coincide (CA is the physically meaningful
   sync point of the two missions).

This is an honest overlay: shapes are preserved (rigid rotation + time shift
only). Remaining differences ARE the model differences — e.g. the physics
Moon is a fixed-radius circle (384,400 km) while the real Moon was near
apogee (~393k-405k km), so the two Moons do not coincide exactly.

Output: venture2_project/Assets/Artemis/nasa_orion_trajectory_aligned.csv
(same 11-column playback format, physics-frame coordinates, physics timeline).
"""
from __future__ import annotations

import csv
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PHYS = ROOT / "venture2_project" / "Assets" / "Artemis" / "orion_trajectory.csv"
NASA = ROOT / "venture2_project" / "Assets" / "Artemis" / "nasa_orion_trajectory.csv"
MOON = ROOT / "data" / "processed" / "moon_ephemeris_horizons.csv"
OUT = ROOT / "venture2_project" / "Assets" / "Artemis" / "nasa_orion_trajectory_aligned.csv"

MOON_RADIUS_KM = 1737.4


def load_rows(path: Path):
    with path.open("r", encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def closest_approach(rows):
    best_i, best_d = 0, float("inf")
    for i, r in enumerate(rows):
        d = math.dist(
            (float(r["orion_x"]), float(r["orion_y"]), float(r["orion_z"])),
            (float(r["moon_x"]), float(r["moon_y"]), float(r["moon_z"])))
        if d < best_d:
            best_i, best_d = i, d
    return best_i, best_d


def cross(a, b):
    return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])


def norm(v):
    return math.sqrt(v[0]*v[0]+v[1]*v[1]+v[2]*v[2])


def unit(v):
    n = norm(v)
    return (v[0]/n, v[1]/n, v[2]/n)


def rot_axis_angle(axis, ang):
    """Rotation matrix, Rodrigues."""
    x, y, z = axis
    c, s, C = math.cos(ang), math.sin(ang), 1-math.cos(ang)
    return (
        (c+x*x*C,   x*y*C-z*s, x*z*C+y*s),
        (y*x*C+z*s, c+y*y*C,   y*z*C-x*s),
        (z*x*C-y*s, z*y*C+x*s, c+z*z*C),
    )


def mat_mul(A, B):
    return tuple(tuple(sum(A[i][k]*B[k][j] for k in range(3)) for j in range(3))
                 for i in range(3))


def apply(R, v):
    return (R[0][0]*v[0]+R[0][1]*v[1]+R[0][2]*v[2],
            R[1][0]*v[0]+R[1][1]*v[1]+R[1][2]*v[2],
            R[2][0]*v[0]+R[2][1]*v[1]+R[2][2]*v[2])


def main() -> None:
    phys = load_rows(PHYS)
    nasa = load_rows(NASA)

    # --- sync point: lunar closest approach in each dataset
    ip, dp = closest_approach(phys)
    inn, dn = closest_approach(nasa)
    t_ca_p = float(phys[ip]["t_sec"])
    t_ca_n = float(nasa[inn]["t_sec"])
    dt = t_ca_p - t_ca_n
    print(f"physics CA: t={t_ca_p:.0f}s alt={dp-MOON_RADIUS_KM:.0f}km | "
          f"NASA CA: t={t_ca_n:.0f}s alt={dn-MOON_RADIUS_KM:.0f}km | shift dt={dt:.0f}s")

    # --- plane: Moon angular momentum around flyby (+-1 day) from Horizons
    moon = load_rows(MOON)
    ls = []
    for r in moon:
        t = float(r["t_sec"])
        if abs(t - t_ca_n) <= 86400:
            rv = (float(r["x_km"]), float(r["y_km"]), float(r["z_km"]))
            vv = (float(r["vx_km_s"]), float(r["vy_km_s"]), float(r["vz_km_s"]))
            ls.append(cross(rv, vv))
    lhat = unit(tuple(sum(c[i] for c in ls)/len(ls) for i in range(3)))

    z = (0.0, 0.0, 1.0)
    ang = math.acos(max(-1.0, min(1.0, lhat[2])))
    axis = cross(lhat, z)
    R1 = rot_axis_angle(unit(axis), ang) if norm(axis) > 1e-12 else \
        ((1, 0, 0), (0, 1, 0), (0, 0, 1))
    print(f"moon orbit plane tilt vs EME2000 equator: {math.degrees(ang):.2f} deg")

    # --- azimuth: match Moon angles at CA
    m_n = apply(R1, (float(nasa[inn]["moon_x"]), float(nasa[inn]["moon_y"]),
                     float(nasa[inn]["moon_z"])))
    th_n = math.atan2(m_n[1], m_n[0])
    th_p = math.atan2(float(phys[ip]["moon_y"]), float(phys[ip]["moon_x"]))
    R2 = rot_axis_angle(z, th_p - th_n)
    R = mat_mul(R2, R1)
    print(f"azimuth match: NASA {math.degrees(th_n):.2f} -> physics {math.degrees(th_p):.2f} deg")

    # --- sanity: Moon must still run counterclockwise after mapping
    m_next = apply(R, (float(nasa[inn+6]["moon_x"]), float(nasa[inn+6]["moon_y"]),
                       float(nasa[inn+6]["moon_z"])))
    m_ca = apply(R, (float(nasa[inn]["moon_x"]), float(nasa[inn]["moon_y"]),
                     float(nasa[inn]["moon_z"])))
    ccw = (math.atan2(m_next[1], m_next[0]) - math.atan2(m_ca[1], m_ca[0])) > 0
    print(f"moon direction after mapping: {'CCW ok' if ccw else 'CW — MISMATCH!'}")

    # --- transform + shift + trim to physics timeline
    t_phys_end = float(phys[-1]["t_sec"])
    out_rows, zs, moon_dist_diff = [], [], []
    for r in nasa:
        t = float(r["t_sec"]) + dt
        if t < 0 or t > t_phys_end:
            continue
        o = apply(R, (float(r["orion_x"]), float(r["orion_y"]), float(r["orion_z"])))
        m = apply(R, (float(r["moon_x"]), float(r["moon_y"]), float(r["moon_z"])))
        v = apply(R, (float(r["orion_vx"]), float(r["orion_vy"]), float(r["orion_vz"])))
        zs.append(abs(m[2]))
        moon_dist_diff.append(norm(m) - 384400.0)
        out_rows.append({
            "t_sec": f"{t:.3f}", "phase": r["phase"],
            "orion_x": f"{o[0]:.3f}", "orion_y": f"{o[1]:.3f}", "orion_z": f"{o[2]:.3f}",
            "moon_x": f"{m[0]:.3f}", "moon_y": f"{m[1]:.3f}", "moon_z": f"{m[2]:.3f}",
            "orion_vx": f"{v[0]:.9f}", "orion_vy": f"{v[1]:.9f}", "orion_vz": f"{v[2]:.9f}",
        })

    with OUT.open("w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=list(out_rows[0].keys()))
        w.writeheader()
        w.writerows(out_rows)

    print(f"wrote {len(out_rows)} rows (trimmed {len(nasa)-len(out_rows)} outside physics timeline) -> {OUT.name}")
    print(f"aligned NASA moon out-of-plane |z|: mean {sum(zs)/len(zs):.0f} km, max {max(zs):.0f} km")
    print(f"real vs physics moon radius: {min(moon_dist_diff):+.0f} .. {max(moon_dist_diff):+.0f} km (physics=384,400 fixed)")


if __name__ == "__main__":
    main()
