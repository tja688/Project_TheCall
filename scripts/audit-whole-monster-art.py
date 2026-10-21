"""Read-only audit of extracted source pixels and baked sprite colorways.

Run after MonsterArtIntegration.Apply() through the Unity CLI. No image is modified.
"""
from pathlib import Path
import argparse
import hashlib
import json
import re

import numpy as np
from PIL import Image


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', default='docs/audit/2026-10-21/whole-monsters/source-audit.json')
    parser.add_argument('--flat-colors', action='store_true', help='Require a uniform fill and no white patches')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    art = root / 'Assets/Arts/Monsters/Whole'
    importer = (root / 'Assets/Editor/MonsterArtIntegration.cs').read_text(encoding='utf-8-sig')
    crops = [tuple(map(int, row)) for row in re.findall(r'new RectInt\((\d+),(\d+),(\d+),(\d+)\)', importer)]
    reference = np.asarray(Image.open(root / 'Assets/示意图/怪物实例.jpg').convert('RGB'))
    errors, rows, hashes = [], [], set()
    for template, (x, y, w, h) in enumerate(crops):
        source = np.asarray(Image.open(art / f'Source-{template}.png').convert('RGBA'))
        if source.shape != (h, w, 4):
            errors.append(f'Wrong crop size: {template}')
            continue
        alpha = source[:, :, 3]
        if np.any(alpha[0]) or np.any(alpha[-1]) or np.any(alpha[:, 0]) or np.any(alpha[:, -1]):
            errors.append(f'Clipped silhouette: {template}')
        opaque = alpha == 255
        delta = np.abs(source[:, :, :3].astype(int) - reference[y:y+h, x:x+w].astype(int)).max(axis=2)
        # Unity and Pillow JPEG decoders can round the original samples differently.
        faithful = int(np.count_nonzero(opaque & (delta <= 5))) / max(1, int(opaque.sum()))
        if faithful < .995:
            errors.append(f'Original opaque pixels changed: {template}: {faithful:.6f}')
        ink = (source[:, :, :3].max(axis=2) < 30) & (alpha > 0)
        for palette in range(6):
            path = art / f'Monster-{template}-{palette}.png'
            pixels = np.asarray(Image.open(path).convert('RGBA'))
            if pixels.shape != source.shape or not np.array_equal(pixels[:, :, 3], alpha):
                errors.append(f'Gradient changed alpha: {template}/{palette}')
            if not np.array_equal(pixels[ink], source[ink]):
                errors.append(f'Gradient changed ink: {template}/{palette}')
            if args.flat_colors:
                rgb = pixels[:, :, :3]
                visible = alpha == 255
                if np.any(visible & (rgb.min(axis=2) > 235)):
                    errors.append(f'White fill remains: {template}/{palette}')
                original_rgb = source[:, :, :3].astype(int)
                value = original_rgb.max(axis=2)
                neutral = value - original_rgb.min(axis=2) <= 12
                dark = (value < 30) | ((value < 64) & neutral)
                near_ink = dark.copy()
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        near_ink |= np.roll(np.roll(dark, dy, axis=0), dx, axis=1)
                interior = visible & ~near_ink
                if len(np.unique(rgb[interior], axis=0)) != 1:
                    errors.append(f'Nonuniform body fill: {template}/{palette}')
            hashes.add(hashlib.sha256(path.read_bytes()).hexdigest())
        rows.append(dict(template=template, width=w, height=h, opaque=int(opaque.sum()),
                         transparent=int((alpha == 0).sum()), faithfulOpaqueRatio=faithful))
    if len(crops) != 12 or len(rows) != 12 or len(hashes) != 72:
        errors.append('Expected twelve extracted drawings and 72 distinct baked gradients')
    # This mid-body shade is almost identical to the backdrop. It must remain opaque.
    twin = np.asarray(Image.open(art / 'Source-3.png').convert('RGBA'))
    if twin[455-crops[3][1], 650-crops[3][0], 3] < 240:
        errors.append('Equal-gray flesh was removed from TwinTendrils')
    report = dict(passed=not errors, source='Assets/示意图/怪物实例.jpg',
                  sourceSha256=hashlib.sha256((root / 'Assets/示意图/怪物实例.jpg').read_bytes()).hexdigest(),
                  drawings=len(rows), colorways=len(hashes), flatColors=args.flat_colors, rows=rows, errors=errors)
    destination = root / args.output
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in report.items() if k not in ('rows', 'errors')} | {'errorCount': len(errors), 'errors': errors[:6]}, ensure_ascii=False))
    if errors:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
