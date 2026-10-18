"""Check real frame traces. Rerun after verify-settlement-visuals.ps1; no rule state is injected here."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
EVIDENCE = ROOT / "docs/audit/2026-10-18/settlement"


def main():
    checks = []
    for name in ["normal", "fast", "skip", "complex-six", "retained", "zero", "tools"]:
        report = json.loads((EVIDENCE / name / "trace.json").read_text(encoding="utf-8-sig"))
        assert report["completed"] and report["allColumnsEmpty"], name
        assert report["expected"] == report["shown"], (name, report["expected"], report["shown"])
        frames = report["frames"]
        assert frames, name
        assert all(not f["captionsOverlap"] for f in frames), (name, "caption overlap or clipping")
        error = max(abs(f["dial"] - min(1, f["energy"] / 50)) for f in frames)
        tank_error = max(abs(f["tank"] - (0.035 + 0.965 * min(1, f["energy"] / 50))) for f in frames)
        assert error < 0.00001 and tank_error < 0.00001, (name, error, tank_error)
        checks.append(dict(check=name + " total, cleanup, continuous needle and tank", passed=True,
                           expected=report["expected"], shown=report["shown"], frames=len(frames),
                           seconds=round(frames[-1]["time"], 3), maximumDialError=error, maximumTankError=tank_error))
    normal = json.loads((EVIDENCE / "normal/trace.json").read_text(encoding="utf-8-sig"))["frames"]
    second = [f for f in normal if f["slot"] == 1]
    assert any(f["phase"] == "support" and f["number"] == 3 for f in second)
    assert any(f["phase"] == "merge" and f["number"] == 3 and f["shards"] > 0 for f in second)
    assert any(f["phase"] == "number" and f["number"] == 4 for f in second)
    charged = [f for f in second if f["phase"] == "charged"]
    assert charged and max(f["fills"][1] for f in charged) > 0.5
    transfer = [f for f in normal if f["slot"] == 2 and 6.001 < f["energy"] < 9.999]
    assert len(transfer) >= 5, "No measured intermediate main-column values"
    fill = max(f["fills"][1] for f in charged)
    conservation_error = max(abs(f["fills"][1] / fill - (10 - f["energy"]) / 4) for f in transfer)
    assert conservation_error < 0.0001, conservation_error
    assert all(f["shards"] == 0 for f in normal if f["phase"] == "zero")
    checks.append(dict(check="honey 3 -> scent -> 4, real glyph fragments, charge held until next seat, proportional drain",
                       passed=True, transferSamples=len(transfer), maximumConservationError=conservation_error))
    complex_frames = json.loads((EVIDENCE / "complex-six/trace.json").read_text(encoding="utf-8-sig"))["frames"]
    assert max(f["pace"] for f in complex_frames) > 1, "Natural acceleration did not occur"
    checks.append(dict(check="existing natural acceleration still runs", passed=True,
                       maximumPace=max(f["pace"] for f in complex_frames)))
    zero = json.loads((EVIDENCE / "zero/trace.json").read_text(encoding="utf-8-sig"))["frames"]
    assert all(f["shards"] == 0 and f["energy"] == 0 and max(f["fills"]) == 0 for f in zero)
    checks.append(dict(check="zero energy floats away without fragments, charging or transfer", passed=True))
    checks.append(dict(check="captions avoid each other, the gauge and screen edges throughout all seven fixtures", passed=True))
    # Pillow is only used for inspection of captured pixels, never for editing assets.
    from PIL import Image, ImageChops, ImageStat
    on = Image.open(EVIDENCE / "column-light-on.png").convert("RGB")
    off = Image.open(EVIDENCE / "column-light-off.png").convert("RGB")
    roi = (305, 805, 368, 932)
    delta = ImageChops.difference(on.crop(roi), off.crop(roi))
    mean = sum(ImageStat.Stat(delta).mean) / 3
    assert mean > 1, ("No rendered Light2D change at the energy column", mean)
    checks.append(dict(check="actual Light2D changes rendered column pixels at fixed fill", passed=True,
                       roi=roi, meanPixelDifference=round(mean, 4)))
    result = dict(passed=True, checks=checks)
    (EVIDENCE / "acceptance.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
