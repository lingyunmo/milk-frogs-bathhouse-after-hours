"""Bake the local-space ash sculpture and its skylight into the shipping 128³ field."""
import gzip
import struct
from pathlib import Path

import numpy as np
from scipy.ndimage import gaussian_filter, map_coordinates, shift

N = 128
rng = np.random.default_rng(870419)
z, y, x = (np.indices((N, N, N), dtype=np.float32) + .5) / N
x -= .5
z -= .5


def noise(x, y, z, frequency):
    width = int(frequency) + 6
    lattice = rng.uniform(-1, 1, (width,) * 3).astype("f")
    # A rotated basis keeps adjacent vortices from revealing the voxel lattice.
    coords = np.stack((.8*z+.6*x, .8*y+.36*z-.48*x, .48*z-.6*y-.64*x)) * frequency + 2
    return map_coordinates(lattice, coords, order=1, mode="grid-wrap", prefilter=False)


# Tilt the rising column, then curl the crown outwards and down around the torus.
x = x - .045 * y * y
r = np.hypot(x, z)
theta = np.arctan2(z, x)
crown = np.exp(-((y - .65) / .16) ** 2)
curl = np.sin(15 * r - 12 * y + 1.2 * np.sin(3 * theta)) * crown
wx = x + .021 * noise(x, y, z, 7) + .016 * curl * np.cos(theta)
wy = y + .034 * noise(z, x, y, 6) + .022 * curl
wz = z + .025 * noise(y, z, x, 7) + .016 * curl * np.sin(theta)


def ellipsoid(cx, cy, cz, sx, sy, sz):
    return (1 - np.sqrt(((wx - cx) / sx) ** 2 + ((wy - cy) / sy) ** 2 +
                        ((wz - cz) / sz) ** 2)) * min(sx, sy, sz)


def merge(a, b, radius):
    return np.maximum(a, b) + np.maximum(radius - np.abs(a - b), 0) ** 2 / (4 * radius)


shape = ellipsoid(0, .677, 0, .236, .157, .23)
for level, count, radius, height, spread in [(0, 11, .22, .656, .126), (1, 19, .28, .633, .083),
                                               (2, 23, .177, .739, .070)]:
    for i in range(count):
        angle = (i + rng.uniform(-.22, .22)) * 2 * np.pi / count + level * .38
        arc = radius + rng.uniform(-.022, .022)
        size = spread * rng.uniform(.82, 1.24)
        lobe = ellipsoid(np.cos(angle) * arc, height + rng.uniform(-.034, .034),
                         np.sin(angle) * arc, size, size * rng.uniform(.83, 1.25), size)
        shape = merge(shape, lobe, .015)

stem_width = .046 + .078 * np.clip(1 - wy / .23, 0, 1) ** 1.7 + .068 * np.clip((wy - .36) / .22, 0, 1) ** 2
stem = np.minimum(stem_width - np.hypot(wx - .011 * np.sin(wy * 31), wz - .018 * np.sin(wy * 19)), .61 - wy)
shape = merge(shape, stem, .026)

# Fine blisters turn the contours and carve soot-filled channels between the larger lobes.
broad = noise(wx, wy, wz, 23)
medium = noise(wx, wy, wz, 47)
fine = noise(wx, wy, wz, 87)
slow_roll = noise(wx, wy, wz, 13)
shape += .030 * slow_roll + .026 * (1 - 1.9 * np.abs(broad)) + .013 * (1 - 2 * np.abs(medium)) + .006 * fine
density = np.clip((shape + .004) / .019, 0, 1)
density *= density * (3 - 2 * density)
# Directional occlusion reaches across neighbouring lobes. Surface relief retains small
# creases even when the game looks straight into the main light.
gy_z, gy_y, gy_x = np.gradient(gaussian_filter(shape, .75), 1 / N)
normal_length = np.maximum(np.sqrt(gy_z**2 + gy_y**2 + gy_x**2), .01)
lambert = np.clip((-gy_x * .53 - gy_y * .78 + gy_z * .33) / normal_length, 0, 1)
optical = np.zeros_like(shape)
for d, weight in [(2, .18), (4, .20), (7, .24), (11, .26), (18, .30)]:
    optical += weight * shift(density, (d * .33, -d * .78, -d * .53), order=1, prefilter=False, mode="constant")
lighting = np.clip(.09 + .91 * (.26 + .74 * lambert) * np.exp(-1.9 * optical), 0, 1)
embers = np.clip(.67 - 1.25 * np.abs(broad + .28 * medium) + .18 * fine, 0, 1)
underbelly = np.exp(-((y - .54) / .095)**2)
embers *= np.clip(1.33 - 1.44 * y + .46 * underbelly, .14, 1) * (.46 + .54 * np.clip(gy_y, 0, 1))
embers += .19 * np.clip(1.2 - y * 2, 0, 1)
strata = np.clip(.55 + broad * .44 + .23 * medium + .10 * fine, 0, 1)
rgba = (np.stack((density, lighting, embers, strata), axis=-1) * 255 + .5).astype("uint8")
rgba[density < .001] = 0
resources = Path(__file__).resolve().parents[1] / "Assets/Resources"


def write_field(label, data):
    data = np.ascontiguousarray(data)
    target = resources / (label + ".brinevolume")
    target.write_bytes(struct.pack("<iB", data.shape[0], data.shape[-1]) +
                       gzip.compress(data.tobytes(), compresslevel=7, mtime=0))
    print(f"{target.name}: {target.stat().st_size:,} bytes")


write_field("BrineCloudField", rgba)

# Periodic dust texture, baked instead of constructing 27 candidate cells per voxel on impact.
from scipy.spatial import cKDTree

dz, dy, dx = np.indices((32, 32, 32), dtype=np.float32) / 4
base = rng.uniform(0, 1, (8, 8, 8))
dust_values = map_coordinates(base, (dz, dy, dx), order=1, mode="grid-wrap", prefilter=False)
lattice = np.indices((8, 8, 8)).reshape(3, -1).T
points = lattice + rng.uniform(.15, .85, lattice.shape)
distance, _ = cKDTree(points, boxsize=8).query(np.stack((dz, dy, dx), axis=-1))
dust = np.stack((dust_values, np.clip(1 - distance * 1.13, 0, 1)), axis=-1)
write_field("BrineDustNoise", (dust * 255 + .5).astype("uint8"))
