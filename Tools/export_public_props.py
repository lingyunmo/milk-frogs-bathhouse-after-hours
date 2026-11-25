"""Convert staged CC0 glTF models and textures for Unity (Blender 5.1)."""
import json
from pathlib import Path
import shutil
import sys
import tempfile
import bpy

root=Path(__file__).resolve().parents[1]
source=Path(sys.argv[sys.argv.index("--")+1])
for path in sorted(source.glob("*/*.gltf")):
    asset=path.stem
    bpy.ops.wm.read_factory_settings(use_empty=True)
    document=json.loads(path.read_text(encoding="utf8"))
    for i, material in enumerate(document["materials"]):
        material["name"] = f"Surface_{i}"
    with tempfile.NamedTemporaryFile(dir=path.parent, prefix="unity-import-", suffix=".gltf", delete=False, mode="w", encoding="utf8") as work:
        json.dump(document,work)
    try:
        bpy.ops.import_scene.gltf(filepath=work.name)
    finally:
        Path(work.name).unlink()
    out=root/"Assets/Resources/Props"/asset
    out.mkdir(parents=True,exist_ok=True)
    surfaces=[]
    def texture(desc):
        if not desc:
            return ""
        image=document["images"][document["textures"][desc["index"]]["source"]]["uri"]
        file=path.parent/image
        shutil.copyfile(file,out/file.name)
        return file.stem
    for i,material in enumerate(document["materials"]):
        name=f"Surface_{i}"
        pbr=material.get("pbrMetallicRoughness",{})
        surfaces.append(dict(name=name,color=texture(pbr.get("baseColorTexture")),
          normal=texture(material.get("normalTexture")),arm=texture(pbr.get("metallicRoughnessTexture")),
          tint=pbr.get("baseColorFactor",[1,1,1,1]),metal=pbr.get("metallicFactor",1),rough=pbr.get("roughnessFactor",1)))
    for o in bpy.context.scene.objects:
        if o.type=="MESH":
            o.select_set(True)
    bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(filepath=str(out/"Model.fbx"),use_selection=True,object_types={"MESH","EMPTY"},
                            mesh_smooth_type="FACE",bake_anim=False,add_leaf_bones=False)
    (out/"Surfaces.json").write_text(json.dumps(dict(surfaces=surfaces),indent=2)+"\n",encoding="utf8")


    if asset == "sofa_02":
        meshes = [obj for obj in bpy.context.selected_objects if obj.type == "MESH"]
        low = min((obj.matrix_world @ v.co).z for obj in meshes for v in obj.data.vertices)
        high = max((obj.matrix_world @ v.co).z for obj in meshes for v in obj.data.vertices)
        frame_height = (high-low)*.23
        frame_clearance, seat_back = .60, .60
        for obj in meshes:
            matrix, inverse = obj.matrix_world.copy(), obj.matrix_world.inverted()
            for vertex in obj.data.vertices:
                point = matrix @ vertex.co
                rise = point.z-low
                if rise <= frame_height:
                    point.z = low + rise/frame_height * frame_clearance
                else:
                    point.z = low + frame_clearance + (rise-frame_height)/(high-low-frame_height)*seat_back
                vertex.co = inverse @ point
        bpy.ops.export_scene.fbx(filepath=str(out/"Raised.fbx"),use_selection=True,
                                object_types={"MESH","EMPTY"},mesh_smooth_type="FACE",bake_anim=False,add_leaf_bones=False)
