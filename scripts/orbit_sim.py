import numpy as np
import pandas as pd

# 物理定数 (SI 単位)
G = 6.67430e-11
M_EARTH = 5.972e24
M_MOON = 7.348e22
MU_EARTH = G * M_EARTH

# 地球-月距離と月の公転速度 (円軌道近似)
EARTH_MOON_DIST = 3.844e8
MOON_ORBITAL_V = np.sqrt(MU_EARTH / EARTH_MOON_DIST)  # 約 1018 m/s

# ===== 初期条件 =====
# 地球は原点に固定 (簡略化: 二体問題で地球質量 >> 月質量、Orion はテスト粒子)
earth_pos = np.array([0.0, 0.0, 0.0])

# 月: Hohmann 遷移で Orion が到達する瞬間に rendezvous する位置からスタート。
# Orion は (r1, 0) から +y 方向に発射 → 半楕円を描いて (-r2, 0) に到達。
# 遷移時間 t_h の間に月が逆方向から会いに来るよう、初期角度を逆算する。
LEO_RADIUS = 7.0e6                                  # Orion 出発半径
a_transfer = (LEO_RADIUS + EARTH_MOON_DIST) / 2     # Hohmann 半長軸
t_transfer = np.pi * np.sqrt(a_transfer**3 / MU_EARTH)  # 約 5 日
moon_omega = np.sqrt(MU_EARTH / EARTH_MOON_DIST**3)     # 月の角速度
moon_angle0 = np.pi - moon_omega * t_transfer            # 初期角度 (約 114 度)

moon_pos = EARTH_MOON_DIST * np.array([np.cos(moon_angle0), np.sin(moon_angle0), 0.0])
moon_vel = MOON_ORBITAL_V * np.array([-np.sin(moon_angle0), np.cos(moon_angle0), 0.0])

# Orion: LEO から TLI (Trans Lunar Injection) 直後の状態。
# Hohmann 遷移の近地点速度 + Artemis 1 実機相当のわずかな超過 (月重力で捕獲される設計)
v_perihelion = np.sqrt(MU_EARTH * (2 / LEO_RADIUS - 1 / a_transfer))
orion_pos = np.array([LEO_RADIUS, 0.0, 0.0])
orion_vel = np.array([0.0, v_perihelion, 0.0])

# ===== シミュレーション設定 =====
dt = 60                                             # 60 秒刻み
total_days = 25                                     # Artemis 1 ミッション期間
steps = int(total_days * 86400 / dt)


def accel_orion(o_pos, m_pos):
    r_e = earth_pos - o_pos
    a_e = MU_EARTH * r_e / np.linalg.norm(r_e)**3
    r_m = m_pos - o_pos
    a_m = G * M_MOON * r_m / np.linalg.norm(r_m)**3
    return a_e + a_m


def accel_moon(m_pos):
    r_e = earth_pos - m_pos
    return MU_EARTH * r_e / np.linalg.norm(r_e)**3


# ===== Velocity Verlet 積分 (シンプレクティック、エネルギー保存性が高い) =====
trajectory = [orion_pos.copy()]
moon_trajectory = [moon_pos.copy()]

a_o = accel_orion(orion_pos, moon_pos)
a_m = accel_moon(moon_pos)

for i in range(steps):
    # ハーフキック
    orion_vel_half = orion_vel + a_o * dt / 2
    moon_vel_half = moon_vel + a_m * dt / 2

    # ドリフト
    orion_pos = orion_pos + orion_vel_half * dt
    moon_pos = moon_pos + moon_vel_half * dt

    # 新位置で加速度再計算
    a_o = accel_orion(orion_pos, moon_pos)
    a_m = accel_moon(moon_pos)

    # ハーフキック (合計フルキック)
    orion_vel = orion_vel_half + a_o * dt / 2
    moon_vel = moon_vel_half + a_m * dt / 2

    trajectory.append(orion_pos.copy())
    moon_trajectory.append(moon_pos.copy())

trajectory = np.array(trajectory)
moon_trajectory = np.array(moon_trajectory)
time_data = np.arange(len(trajectory)) * dt  # 単位: 秒

# ===== CSV 出力 (Unity TrajectoryLoader 用) =====
df = pd.DataFrame({
    "time": time_data,
    "x": trajectory[:, 0],
    "y": trajectory[:, 1],
    "z": trajectory[:, 2],
})
df.to_csv("output/artemis1_trajectory.csv", index=False)

# Unity 側で月もアニメーションさせる場合に使えるよう別ファイルで出力
df_moon = pd.DataFrame({
    "time": time_data,
    "x": moon_trajectory[:, 0],
    "y": moon_trajectory[:, 1],
    "z": moon_trajectory[:, 2],
})
df_moon.to_csv("output/moon_trajectory.csv", index=False)

# ===== サマリーを表示 (デバッグ用) =====
max_dist = np.max(np.linalg.norm(trajectory, axis=1))
final_dist = np.linalg.norm(trajectory[-1])
closest_approach = np.min(np.linalg.norm(trajectory - moon_trajectory, axis=1))

print("CSV保存完了")
print(f"  シミュレーション期間: {total_days} 日 ({len(df)} ステップ)")
print(f"  地球からの最大距離: {max_dist/1000:.0f} km")
print(f"  地球からの最終距離: {final_dist/1000:.0f} km")
print(f"  月への最接近距離: {closest_approach/1000:.0f} km")
