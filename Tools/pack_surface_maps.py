"""Pack roughness and Poly Haven ARM maps into Unity Standard texture channels."""
import json
from pathlib import Path
from PIL import Image, ImageOps

resources = Path(__file__).resolve().parents[1] / "Assets/Resources"

for info in resources.glob("Props/*/Surfaces.json"):
    document = json.loads(info.read_text(encoding="utf8"))
    for surface in document["surfaces"]:
        name = surface["arm"]
        if not name or name.endswith("_Unity"):
            continue
        old = next((p for p in info.parent.glob(name+".*") if p.suffix in (".jpg",".png")), None)
        packed = info.parent / (name + "_Unity.png")
        if old:
            with Image.open(old) as image:
                ao, rough, metal = image.convert("RGB").split()
                Image.merge("RGBA", (metal, ao, metal, ImageOps.invert(rough))).save(packed)
        assert packed.exists(), packed
        surface["arm"] = packed.stem
    info.write_text(json.dumps(document,indent=2)+"\n",encoding="utf8")

for rough in (resources/"Finishes").glob("*Rough.jpg"):
    with Image.open(rough) as image:
        smooth = ImageOps.invert(image.convert("L"))
        fill = Image.new("L", smooth.size, 0)
        Image.merge("RGBA", (fill,fill,fill,smooth)).save(rough.with_name(rough.stem.replace("Rough","Finish")+".png"))
