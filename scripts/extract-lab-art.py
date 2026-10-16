"""Rebuild the lab's lossless PNGs from the delivered PSD. Run with uv run --with psd-tools.

Layer indices are checked against names so a revised PSD fails visibly instead of using
the wrong painting. Pixel positions in manifest.json use the PSD's top-left origin.
"""
import hashlib
import json
from pathlib import Path

from psd_tools import PSDImage
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets/Arts/Psds/游玩主界面.psd"
OUTPUT = ROOT / "Assets/Arts/Lab"
LAYERS = {
    "LightBreeding2": (30, "图层 29"),
    "LightBreeding1": (31, "图层 31"),
    "LightProduction1": (32, "图层 34"),
    "LightProduction2": (33, "图层 35"),
    "CollectorGlow": (34, "图层 36"),
    "RecyclerGlow": (35, "图层 37"),
    "CollectorNeedle": (36, "图层 38"),
    "BossReserved": (38, "图层 6"),
}


def main():
    psd = PSDImage.open(SOURCE)
    assert psd.size == (1920, 1080), "PSD layout changed. Review the scene mapping."
    layers = list(psd.descendants())
    for index, name in LAYERS.values():
        assert layers[index].name == name, (index, layers[index].name, name)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    excluded = {id(layers[index]) for index, _ in LAYERS.values()}
    psd.composite(layer_filter=lambda layer: layer.is_visible() and id(layer) not in excluded).save(OUTPUT / "LabShell.png")
    manifest = {"source": "Assets/Arts/Psds/游玩主界面.psd", "sha256": hashlib.sha256(SOURCE.read_bytes()).hexdigest(), "size": list(psd.size), "layers": []}
    for name, (index, original) in LAYERS.items():
        layer = layers[index]
        picture = layer.composite().convert("RGBA")
        # Layer.composite returns local pixel coordinates; opacity is applied here once.
        if layer.opacity != 255:
            picture.putalpha(picture.getchannel("A").point(lambda a: round(a * layer.opacity / 255)))
        picture.save(OUTPUT / (name + ".png"))
        manifest["layers"].append({"name": name, "sourceLayer": original, "index": index, "bounds": list(layer.bbox)})
    # The PSD has one zigzag mouth. Split this authored strip into movable jaws.
    shell = Image.open(OUTPUT / "LabShell.png")
    shell.crop((1537, 610, 1794, 684)).save(OUTPUT / "RecyclerClosed.png")
    shell.crop((1537, 610, 1794, 647)).save(OUTPUT / "RecyclerUpperJaw.png")
    shell.crop((1537, 647, 1794, 684)).save(OUTPUT / "RecyclerLowerJaw.png")
    shell.crop((150, 200, 690, 478)).save(OUTPUT / "Room2Interior.png")
    layers[39].composite().save(OUTPUT / "LabFrame.png")
    Image.open(ROOT / "Assets/Art/Ui/BgProduction.png").crop((90, 381, 280, 430)).save(OUTPUT / "ProductionPlatform.png")
    (OUTPUT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Exported lab shell and 9 lossless layers; source PSD preserved.")


if __name__ == "__main__":
    main()
