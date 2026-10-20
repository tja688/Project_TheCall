"""Inventory the supplied monster artwork without modifying any source asset."""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
import sys
sys.stdout.reconfigure(encoding="utf-8")
from collections import Counter
from pathlib import Path
from zipfile import ZipFile

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
PARTS = Path("Assets/Arts/Pngs/部位组件")
PSB = Path("Assets/Arts/Psds/怪物拼接件.psb")
REFERENCES = (
    Path("Assets/示意图/怪物核心视觉示意图.jpg"),
    Path("Assets/示意图/怪物拼接草图.jpg"),
)
DESIGN = Path("Assets/Notes/怪物性格遗传与技能设计参考.docx")
DEFAULT_OUTPUT = Path("docs/audit/2026-10-20/monsters/art-inventory.json")
EXPECTED_GROUP_COUNTS = {
    "01_眼睛": 10,
    "04_嘴与外舌": 7,
    "05_口腔与牙齿": 7,
    "06_手脚与触肢": 14,
    "07_角翼与配饰": 14,
    "08_表情与花纹": 5,
    "身体组件": 13,
}


def fingerprint(path: Path) -> dict:
    absolute = ROOT / path
    digest = hashlib.sha256()
    with absolute.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return {
        "path": path.as_posix(),
        "bytes": absolute.stat().st_size,
        "sha256": digest.hexdigest(),
    }


def image_record(path: Path) -> dict:
    record = fingerprint(path)
    with Image.open(ROOT / path) as image:
        image.load()
        record.update(format=image.format, mode=image.mode, size=list(image.size))
        if "A" in image.getbands():
            alpha = image.getchannel("A")
            bounds = alpha.getbbox()
            record["alpha_bounds"] = list(bounds) if bounds else None
            record["visible_size"] = (
                [bounds[2] - bounds[0], bounds[3] - bounds[1]] if bounds else [0, 0]
            )
            record["alpha_extrema"] = list(alpha.getextrema())
    return record


def collect() -> tuple[dict, list[str]]:
    errors = []
    files = sorted((ROOT / PARTS).rglob("*.png"))
    counts = Counter(path.parent.name for path in files)
    if dict(counts) != EXPECTED_GROUP_COUNTS:
        errors.append(f"PNG category counts changed: {dict(counts)}")

    components = []
    for absolute in files:
        record = image_record(absolute.relative_to(ROOT))
        if record.get("mode") != "RGBA":
            errors.append(f"Component is not RGBA: {record['path']}")
        if not record.get("alpha_bounds"):
            errors.append(f"Component has no visible pixels: {record['path']}")
        extrema = record.get("alpha_extrema", [])
        if len(extrema) != 2 or extrema[0] != 0 or extrema[1] <= 0:
            errors.append(f"Component lost transparency or visible pixels: {record['path']}")
        components.append(record)

    psb = fingerprint(PSB)
    with (ROOT / PSB).open("rb") as stream:
        signature, version, _, channels, height, width, depth, color_mode = struct.unpack(
            ">4sH6sHIIHH", stream.read(26)
        )
    if signature != b"8BPS" or version != 2:
        errors.append("Original monster document is not a Photoshop PSB v2 file")
    psb.update(
        signature=signature.decode("ascii"),
        version=version,
        size=[width, height],
        channels=channels,
        depth=depth,
        color_mode=color_mode,
    )
    design = fingerprint(DESIGN)
    with ZipFile(ROOT / DESIGN) as archive:
        design["embedded_images"] = len(
            [name for name in archive.namelist() if name.startswith("word/media/")]
        )
    report = {
        "schema": 1,
        "source_png_count": len(components),
        "category_counts": dict(sorted(counts.items())),
        "portrait_design_size": [279, 299],
        "psb": psb,
        "references": [image_record(path) for path in REFERENCES],
        "design_document": design,
        "components": components,
        "warnings": [
            "neck_curve_942.png has asymmetric transparent padding; use alpha bounds or an authored pivot.",
            "The PSB is an artwork board, not one runtime monster; its visible layers include overlapping examples.",
            "White fills are tintable; eyes, teeth, and black outlines need independent color treatment.",
        ],
        "errors": errors,
    }
    return report, errors


def compare(previous: dict, current: dict) -> list[str]:
    if previous == current:
        return []
    old_sources = {entry["path"]: entry for entry in previous.get("components", [])}
    new_sources = {entry["path"]: entry for entry in current["components"]}
    changes = []
    for path in sorted(set(old_sources) | set(new_sources)):
        if old_sources.get(path) != new_sources.get(path):
            changes.append(f"Component changed: {path}")
    for key in ("schema", "category_counts", "psb", "references", "design_document"):
        if previous.get(key) != current.get(key):
            changes.append(f"Source metadata changed: {key}")
    return changes or ["Inventory report changed; regenerate and review the JSON diff"]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Compare sources with the saved inventory")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()
    output = args.output if args.output.is_absolute() else ROOT / args.output
    report, errors = collect()
    if args.check:
        if not output.is_file():
            errors.append(f"Inventory does not exist: {output}")
        else:
            errors.extend(compare(json.loads(output.read_text(encoding="utf-8")), report))
    else:
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({
        "ok": not errors,
        "mode": "check" if args.check else "write",
        "source_png_count": report["source_png_count"],
        "category_counts": report["category_counts"],
        "output": str(output),
        "errors": errors,
    }, ensure_ascii=False, indent=2))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
