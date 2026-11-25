"""Rebuild the Unity creature from the checked-in Blender source without changing it."""
from collections import Counter, defaultdict
import math
from pathlib import Path
import shutil
import bpy
from mathutils import Vector

root = Path(__file__).resolve().parents[1]
source = root / "ArtSource/HorrorMilkFrog/haunted-fullbody-skin-03"
output = root / "Assets/Resources/Creature"
bpy.ops.wm.open_mainfile(filepath=str(source / "Horror-Milk-Frog-03.blend"))
bpy.context.scene.frame_set(1)
body = next(obj for obj in bpy.context.scene.objects if obj.name.startswith("01 |"))

edge_use = Counter(tuple(sorted(edge)) for face in body.data.polygons for edge in face.edge_keys)
links = defaultdict(list)
for (a, b), count in edge_use.items():
    if count == 1:
        links[a].append(b)
        links[b].append(a)
assert links and all(len(neighbours) == 2 for neighbours in links.values())
rim = [min(links)]
previous = None
while True:
    following = next(point for point in links[rim[-1]] if point != previous)
    if following == rim[0]:
        break
    previous = rim[-1]
    rim.append(following)
assert len(rim) == len(links) == 164
basis, open_pose = body.data.shape_keys.key_blocks
loops = [[key.data[i].co.copy() for i in rim] for key in (basis, open_pose)]

# Match both lip shapes vertex for vertex and use the same blend weight for the lining.
bands = ((1.025, .012), (1, .0), (.968, .037), (.90, .105), (.73, .21), (.45, .38), (.18, .49))
n = len(rim)
def cavity_points(loop):
    centre = sum(loop, Vector()) / n
    points = []
    for radius, inset in bands:
        for point in loop:
            x = centre.x + (point.x-centre.x)*radius
            z = centre.z + (point.z-centre.z)*radius
            y = point.y*radius + centre.y*(1-radius) + inset
            points.append((x, y, z))
    return points + [(centre.x, centre.y+.55, centre.z)]

def signed_area(loop):
    return sum(a.x*b.z-b.x*a.z for a, b in zip(loop,loop[1:]+loop[:1]))

faces = []
for row in range(len(bands)-1):
    for i in range(n):
        a, b = row*n + i, row*n + (i+1)%n
        faces.append((a,b,b+n,a+n))
for i in range(n):
    faces.append(((len(bands)-1)*n+i, (len(bands)-1)*n+(i+1)%n, len(bands)*n))
if signed_area(loops[0]) < 0:
    faces = [tuple(reversed(face)) for face in faces]

lining = bpy.data.meshes.new("Recessed oral lining")
lining.from_pydata(cavity_points(loops[0]), [], faces)
lining.update()
mouth = bpy.data.objects.new("Mouth lining", lining)
bpy.context.scene.collection.objects.link(mouth)
mouth.parent = body.parent
mouth.matrix_world = body.matrix_world.copy()
lining.materials.append(bpy.data.materials.new("Mucosa"))
uv = lining.uv_layers.new(name="Mucosa")
depth_uv = lining.uv_layers.new(name="Oral depth")
for poly in lining.polygons:
    poly.use_smooth = True
    for li in poly.loop_indices:
        index = lining.loops[li].vertex_index
        vertex = lining.vertices[index].co
        uv.data[li].uv = ((vertex.x+.9)/1.8, (vertex.z-.14)/1.24)
        radius = bands[index // n][0] if index < n*len(bands) else 0
        depth_uv.data[li].uv = (1-min(1,radius), .5)
mouth.shape_key_add(name="Grin")
key = mouth.shape_key_add(name="Belly laugh")
for point, co in zip(key.data,cavity_points(loops[1])):
    point.co = co
for i, expected in enumerate(loops):
    mouth_keys = mouth.data.shape_keys.key_blocks[i]
    for j, rim_point in enumerate(expected):
        exported_edge = mouth.matrix_world @ mouth_keys.data[n+j].co
        assert (exported_edge - body.matrix_world @ rim_point).length < .00001


def fit_inside(point, loop):
    x, z = point.x, point.z
    loop2 = [Vector((v.x,v.z)) for v in loop]
    inside = False
    projected = Vector((x,z))
    closest, dist = None, float("inf")
    for a, b in zip(loop2,loop2[1:]+loop2[:1]):
        if (a.y>z) != (b.y>z) and x < (b.x-a.x)*(z-a.y)/(b.y-a.y)+a.x:
            inside = not inside
        edge = b-a
        t = max(0,min(1,(projected-a).dot(edge)/max(1e-8,edge.length_squared)))
        hit = a+t*edge
        distance = (hit-projected).length_squared
        if distance < dist:
            closest,dist = hit,distance
    if not inside:
        centre = sum(loop2,Vector((0,0)))/n
        closest = closest.lerp(centre,.024)
        point.x,point.z = closest

for obj in list(bpy.context.scene.objects):
    if obj.name.startswith(("Open mouth |","Upper gums","Lower gums")):
        bpy.data.objects.remove(obj,do_unlink=True)
        continue
    if " tooth " not in obj.name and not obj.name.startswith("Tongue"):
        continue
    for j, pose in enumerate(obj.data.shape_keys.key_blocks):
        for point in pose.data:
            fit_inside(point.co,loops[j])
            point.co.y += .052 if " tooth " in obj.name else .10
            if obj.name.startswith("Tongue"):
                point.co.z += .023

neutral = body.data.shape_keys.key_blocks[0]
for name, side in (("Stride A",1),("Stride B",-1)):
    gait = body.shape_key_add(name=name,from_mix=False)
    for point, target in zip(neutral.data,gait.data):
        x,y,z = point.co
        lower = max(0,min(1,(-z-1.38)/1.6))
        leg = math.tanh(x*3)*side
        target.co.y = y+lower*.47*leg
        target.co.z = z+lower*.26*max(0,leg)

bpy.ops.object.select_all(action="DESELECT")
for obj in bpy.context.scene.objects:
    if obj.type != "MESH" or not obj.visible_get():
        continue
    obj.select_set(True)
    if obj.data.shape_keys and obj.data.shape_keys.animation_data:
        obj.data.shape_keys.animation_data_clear()
    if obj.data.shape_keys:
        for shape in list(obj.data.shape_keys.key_blocks)[1:]:
            shape.value=0
    if obj == body:
        for layer in list(obj.data.uv_layers):
            if layer.name != "Skin atlas":
                obj.data.uv_layers.remove(layer)
bpy.ops.export_scene.fbx(filepath=str(output/"MilkFrog.fbx"),use_selection=True,
                        object_types={"MESH"},mesh_smooth_type="FACE",bake_anim=False,add_leaf_bones=False)
for original, name in (("Oral-mucosa.png","Mucosa.png"),("Sulfur-iris.png","Iris.png")):
    shutil.copyfile(source/original,output/name)
print(f"Connected mouth to {n} rim vertices at both endpoints")
