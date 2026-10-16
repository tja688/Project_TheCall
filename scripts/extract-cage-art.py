"""Separate the delivered cage painting into moving doors and a fixed aperture.

Run: uv run --with psd-tools python -X utf8 scripts/extract-cage-art.py
No repainting or source PSD changes. Coordinates are in the original 1920x1080 PSD.
"""
from pathlib import Path
import hashlib
import json

from PIL import Image, ImageChops
from psd_tools import PSDImage

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets/Arts/Lab/Cage"
SOURCE = ROOT / "Assets/Arts/Psds/游玩主界面.psd"


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    shell = Image.open(ROOT / "Assets/Arts/Lab/LabShell.png").convert("RGBA")
    psd = PSDImage.open(SOURCE)
    layers = list(psd.descendants())
    assert layers[6].name == "图层 11" and layers[19].name == "图层 18"
    ink = layers[6].composite().convert("RGBA")
    bars = Image.new("RGBA", shell.size)
    layer = layers[19]
    bars.paste(layer.composite(), (layer.left, layer.top))
    for row in range(2):
        box = (1538, 158 + row * 140, 1802, 298 + row * 140)
        back = shell.crop(box)
        # The base layer is opaque: isolate its painted dark iron, then union the
        # actual transparent bar layer. The final composite supplies graded RGB.
        rgb = ink.crop(box).convert("RGB")
        mask = Image.new("L", rgb.size)
        mask.putdata([round(255 * max(0, min(1, (84 - max(pixel)) / 14))) for pixel in rgb.getdata()])
        bar_crop = bars.crop(box)
        bar_ink = Image.new("L", rgb.size)
        bar_ink.putdata([round(a * max(0, min(1, (84 - max(r, g, b)) / 14)))
                        for r, g, b, a in bar_crop.getdata()])
        # The broad pale strokes in layer 18 belong behind the occupant. Only
        # its iron cores occlude the monster, keeping faces and poses readable.
        mask = ImageChops.lighter(mask, bar_ink)
        front = back.copy()
        front.putalpha(mask)
        back.save(OUT / f"RowBack{row}.png")
        front.save(OUT / f"RowFront{row}.png")
    frame = shell.crop((1526, 138, 1814, 462))
    frame.paste((0, 0, 0, 0), (12, 20, 276, 300))
    frame.save(OUT / "CabinetRim.png")
    shade = Image.new("RGBA", (8, 40))
    shade.putdata([(255, 255, 255, round(205 * (1 - y / 39) ** 2)) for y in range(40) for _ in range(8)])
    shade.save(OUT / "EdgeShade.png")
    arrow = shell.crop((1590, 878, 1627, 929))
    # Use the actual green painted arrow, dropping its opaque panel background.
    arrow.putalpha(arrow.convert("RGB").point(lambda x: x).getchannel("G").point(lambda g: 255 if g > 90 else 0))
    arrow.transpose(Image.Transpose.ROTATE_270).save(OUT / "ArrowUp.png")
    arrow.transpose(Image.Transpose.ROTATE_90).save(OUT / "ArrowDown.png")
    hidden = {id(layers[21]), id(layers[22]), id(layers[38])}
    # Remove the old remote cage arrows from the gene cabinet once controls
    # move beside the actual cage. Preserve the painted panel and centre tile.
    psd.composite(layer_filter=lambda layer: layer.is_visible() and id(layer) not in hidden).crop(
        (1538, 872, 1802, 936)).save(OUT / "OldNavigationPanel.png")
    manifest = {"source": str(SOURCE.relative_to(ROOT)), "sha256": hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
                "aperture": [1538, 158, 264, 280], "rowHeight": 140,
                "foregroundLayers": [6, 19], "description": "Original RGB with iron/bar alpha; two authored rows alternate."}
    (OUT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Exported two painted cage rows, foreground iron, fixed rim, shadow and arrows.")


if __name__ == "__main__":
    main()
