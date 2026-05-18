import numpy as np
import pandas as pd
import matplotlib.pyplot as plt

G = 6.67430e-11
M_EARTH = 5.972e24
M_MOON = 7.348e22

earth_pos = np.array([0.0,0.0,0.0])
moon_pos = np.array([384400000.0,0.0,0.0])

orion_pos = np.array([7000000.0,0.0,0.0])
orion_vel = np.array([0.0,10800.0,0.0])

dt = 60
steps = 50000

trajectory = []

for i in range(steps):

   r_earth = earth_pos - orion_pos
   d_earth = np.linalg.norm(r_earth)
   a_earth = G * M_EARTH * r_earth / (d_earth**3)

   r_moon = moon_pos - orion_pos
   d_moon = np.linalg.norm(r_moon)
   a_moon = G * M_MOON * r_moon / (d_moon**3)

   total_acc = a_earth + a_moon

   orion_vel += total_acc * dt
   orion_pos += orion_vel * dt

   trajectory.append(orion_pos.copy())


trajectory = np.array(trajectory)

plt.figure(figsize=(8,8))
plt.plot(trajectory[:,0], trajectory[:,1])
plt.scatter(0,0,label="Earth")
plt.scatter(384400000,0,label="Moon")
plt.legend()
plt.axis("equal")
plt.show()
