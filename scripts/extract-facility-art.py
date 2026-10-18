"""Lossless layer extraction for the October horizontal facility delivery.

Run: uv run --with psd-tools python -X utf8 scripts/extract-facility-art.py
The PSD is read only. Coordinates remain in its 1920x1080 room coordinate system.
"""
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageOps
from psd_tools import PSDImage

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets/Arts/Psds/横向地图分层拆解.psd"
OUT = ROOT / "Assets/Arts/Facility"
WORLD = (739, 605, 4579, 1685)
SHOP = (2659, 1766, 4579, 2846)


def main():
    psd = PSDImage.open(SOURCE)
    assert psd.size == (4992, 3128), "Review coordinates for the revised delivery."
    layers = list(psd.descendants())
    expected = {58: "整体横向地图", 68: "神秘几何", 99: "怪物", 111: "培养室",
                126: "基座", 132: "卷帘门", 172: "多边形 1", 200: "灯光",
                245: "怪", 254: "笼子", 267: "回收站", 282: "分区色"}
    for index, name in expected.items():
        assert layers[index].name == name, (index, name, layers[index].name)
    OUT.mkdir(parents=True, exist_ok=True)
    entries = []

    def blocked(indices):
        excluded = set()
        for i in indices:
            excluded.add(id(layers[i]))
            if layers[i].is_group():
                excluded.update(id(child) for child in layers[i].descendants())
        return excluded

    def save(name, picture, bounds, source_indices):
        picture = picture.convert("RGBA")
        # Flat albedo retains the artist's luminance. Palette and light are editable in Unity.
        alpha = picture.getchannel("A")
        picture = ImageOps.grayscale(picture).convert("RGBA")
        picture.putalpha(alpha)
        if name.startswith("Monster"):
            # The source silhouettes are black. A white albedo lets the engine recolor them
            # without changing their outline or multiplying every palette back to black.
            picture = Image.new("RGBA", picture.size, "white")
            picture.putalpha(alpha)
        picture.save(OUT / (name + ".png"))
        entries.append(dict(name=name, bounds=list(bounds), sources=source_indices,
                            sha256=hashlib.sha256((OUT / (name + ".png")).read_bytes()).hexdigest()))

    def export(name, index, origin=WORLD, exclude=()):
        layer = layers[index]
        skip = blocked(exclude)
        picture = layer.composite(layer_filter=lambda child: child.visible and id(child) not in skip)
        if picture is None:
            raise ValueError((index, layer.name))
        if layer.opacity != 255:
            picture.putalpha(picture.getchannel("A").point(lambda a: round(a * layer.opacity / 255)))
        x, y, right, bottom = layer.bbox
        save(name, picture, (x-origin[0], y-origin[1], right-origin[0], bottom-origin[1]), [index])

    # Every pictured creature is removed from the scenery. Only actual owned monsters are drawn.
    skip = blocked([60, 68, 69, 70, 71, 93, 98, 99, 111, 126, 132, 172, 200, 245,
                    250, 251, 252, 253, 254, 255, 256, 257, 260, 266, 269, 282])
    painting = psd.composite(layer_filter=lambda l: l.is_visible() and id(l) not in skip)
    world = painting.crop(WORLD)
    save("DormShell", world.crop((0, 0, 1920, 1080)), (0, 0, 1920, 1080), [58])
    save("ProductionShell", world.crop((1920, 0, 3840, 1080)), (1920, 0, 3840, 1080), [58])
    save("ShopShell", painting.crop(SHOP), (0, 0, 1920, 1080), [3])
    # Only the operation screen relocates its gauge. Other screens keep their original background.
    work_skip = skip | blocked([150])
    work_painting = psd.composite(layer_filter=lambda l: l.is_visible() and id(l) not in work_skip)
    save("ProductionWorkShell", work_painting.crop((WORLD[0]+1920, WORLD[1], WORLD[0]+3840, WORLD[3])),
         (1920, 0, 3840, 1080), [58])
    for name, index in [("DormGeometry", 68), ("DormTelevision", 69), ("DormCandy", 70),
                        ("DormPlant", 71), ("Platform", 127), ("Shutter", 132),
                        ("GaugeNeedle", 172), ("CollectorGlow", 98), ("CollectorCore", 93), ("CageDoor", 254),
                        ("CageBack", 242), ("RecyclerMouth", 267), ("RecyclerLip", 268),
                        ("BreedingPod", 114), ("BreedingTubeLeft", 115), ("BreedingTubeRight", 117),
                        ("BreedingChild", 118), ("BreedingChildRim", 120), ("GenePlus", 123),
                        ("LampCone", 201), ("LampTube", 212)]:
        export(name, index)
    for j, i in enumerate([100, 103, 105, 107, 110]):
        export("Monster%02d" % (j+1), i)
    export("GaugeHousing", 150, exclude=(172,))
    for name, part in [("CageRowBack", "CageBack"), ("CageRowFront", "CageDoor")]:
        picture = Image.open(OUT / (part + ".png"))
        row = Image.new("RGBA", (344, 154))
        row.alpha_composite(picture, (0, 0))
        row.alpha_composite(picture, (174, 0))
        save(name, row, (0, 0, 344, 154), [242 if part == "CageBack" else 254])
    # Keep the authored tooth silhouette; independent jaws move over the real aperture.
    mouth = Image.open(OUT / "RecyclerMouth.png")
    w, h = mouth.size
    save("RecyclerUpper", mouth.crop((0, 0, w, h//2)), (3368, 559, 3728, 559+h//2), [267])
    save("RecyclerLower", mouth.crop((0, h//2, w, h)), (3368, 559+h//2, 3728, 684), [267])
    manifest = dict(source=SOURCE.relative_to(ROOT).as_posix(),
                    sha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest(), size=list(psd.size),
                    panoramaOrigin=list(WORLD[:2]), shopOrigin=list(SHOP[:2]), layers=entries)
    (OUT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    print(f"Exported {len(entries)} authored layers; PSD SHA-256 {manifest['sha256']}")


if __name__ == "__main__":
    main()
