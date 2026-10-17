"""Separate concatenated QA runs without changing any recorded state.

The legacy recorder appended to an existing file. Preserve those raw bytes and
extract the last session only when that complete session exactly equals the
independently recorded pre-art baseline. Never repair or rewrite state values.
"""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
EVIDENCE = ROOT / "docs/audit/2026-10-17/facility"
BASELINE = ROOT / "docs/audit/2026-10-15/lab/natural-0/state-trace.txt"
MARKER = b"begin\nphase=Opening;day=0;"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", type=int, choices=(0, 1, 2), action="append")
    args = parser.parse_args()
    reports = []
    prior = EVIDENCE / "trace-session-splits.json"
    if prior.exists():
        reports = json.loads(prior.read_text(encoding="utf-8"))
    expected = BASELINE.read_bytes()
    for mode in args.mode or (0, 1, 2):
        path = EVIDENCE / f"natural-{mode}/state-trace.txt"
        raw = path.read_bytes()
        count = raw.count(MARKER)
        if count <= 1:
            continue
        offset = raw.rfind(MARKER)
        session = raw[offset:]
        assert session == expected, "Last complete session differs; raw data left untouched."
        digest = hashlib.sha256(raw).hexdigest()
        archive = EVIDENCE / "replay-history" / f"mode-{mode}-{digest[:12]}.txt"
        archive.parent.mkdir(parents=True, exist_ok=True)
        archive.write_bytes(raw)
        path.write_bytes(session)
        reports.append({"mode": mode, "rawFile": archive.relative_to(ROOT).as_posix(),
                        "rawSha256": digest, "sessions": count, "lastSessionByteOffset": offset,
                        "lastSessionSha256": hashlib.sha256(session).hexdigest(),
                        "exactBaselineMatchBeforeExtraction": True})
        print(f"Preserved {count} raw sessions; extracted byte-identical completed run for mode {mode}.")
    if reports:
        prior.write_text(json.dumps(reports, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")


if __name__ == "__main__":
    main()
