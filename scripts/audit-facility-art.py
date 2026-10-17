"""Independently check delivery integrity and the captured engine-light difference."""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image
from psd_tools import PSDImage

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets/Arts/Facility"
EVIDENCE = ROOT / "docs/audit/2026-10-17/facility"


def main():
    manifest = json.loads((ART / "manifest.json").read_text(encoding="utf-8"))
    source = ROOT / manifest["source"]
    assert hashlib.sha256(source.read_bytes()).hexdigest() == manifest["sha256"], "Source PSD changed"
    checks = []
    for entry in manifest["layers"]:
        path = ART / (entry["name"] + ".png")
        assert hashlib.sha256(path.read_bytes()).hexdigest() == entry["sha256"], path
    checks.append({"check": "source PSD and all exported-layer hashes", "count": len(manifest["layers"]), "passed": True})
    psd = PSDImage.open(source)
    source_layers = list(psd.descendants())
    silhouettes = []
    for name, index in [("Monster01", 100), ("Monster02", 103), ("Monster03", 105), ("Monster04", 107), ("Monster05", 110)]:
        original = source_layers[index].composite().convert("RGBA")
        actual = Image.open(ART / (name + ".png")).convert("RGBA")
        assert actual.size == original.size
        assert actual.getchannel("A").tobytes() == original.getchannel("A").tobytes(), name
        pixels = np.asarray(actual)
        assert np.all(pixels[pixels[:, :, 3] > 0, :3] == 255), "Tintable albedo not white"
        silhouettes.append({"name": name, "sourceLayer": index, "size": list(actual.size), "exactAuthoredAlpha": True})
    checks.append({"check": "five whole silhouettes preserve the authored outlines", "passed": True, "silhouettes": silhouettes})

    on = np.asarray(Image.open(EVIDENCE / "03-production-expanded.png").convert("RGB"), dtype=float)
    off = np.asarray(Image.open(EVIDENCE / "lighting-off.png").convert("RGB"), dtype=float)
    # An empty part of the second breeding room, below its lamp, excludes UI text and monsters.
    roi = (1000, 225, 1180, 420)
    x1, y1, x2, y2 = roi
    delta = (on - off)[y1:y2, x1:x2]
    mean = float(delta.mean())
    changed = float((abs(delta).max(axis=2) > 4).mean())
    assert mean > 2 and changed > 0.2, (mean, changed)
    checks.append({"check": "real Light2D toggle changes rendered room illumination", "passed": True,
                   "roi": list(roi), "meanChannelBrightnessGain": round(mean, 3), "changedPixelFraction": round(changed, 4)})
    for path in EVIDENCE.glob("0*.png"):
        assert Image.open(path).size == (1920, 1080), path
    if (EVIDENCE / "aspect-2560-1080.png").exists():
        wide = np.asarray(Image.open(EVIDENCE / "aspect-2560-1080.png").convert("RGB"))
        tall = np.asarray(Image.open(EVIDENCE / "aspect-1920-1200.png").convert("RGB"))
        borders = [int(wide[:, :300].max()), int(wide[:, 2260:].max()),
                   int(tall[:45].max()), int(tall[1155:].max())]
        assert max(borders) < 32, "Another room leaked beyond the authored viewport"
        checks.append({"check": "wide and tall viewports mask adjacent-room art outside their bounds",
                       "passed": True, "maximumBorderChannels": borders})
    report = {"checks": checks, "passed": True, "sourceSha256": manifest["sha256"]}
    (EVIDENCE / "art-integrity.json").write_text(json.dumps(report, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    print(json.dumps({"passed": True, "exportedLayers": len(manifest["layers"]), "lightBrightnessGain": round(mean, 3), "lightChangedPixelFraction": round(changed, 4)}))


if __name__ == "__main__":
    main()
