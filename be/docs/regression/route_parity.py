#!/usr/bin/env python3
"""Route parity check — live OpenAPI vs docs/route-baseline.txt (Phase 0 gate).

Usage:
    python docs/regression/route_parity.py --openapi http://localhost:5000/openapi/v1.json \
        --baseline docs/route-baseline.txt

Exit code 0 = parity OK (62=62), 1 = drift (missing/extra routes).
Trailing slashes dinormalisasi; baris komentar (#) dan kosong diabaikan.
"""
import argparse
import json
import sys
import urllib.request


def fetch_openapi(source: str) -> dict:
    if source.startswith("http://") or source.startswith("https://"):
        with urllib.request.urlopen(source, timeout=15) as resp:
            return json.load(resp)
    with open(source, encoding="utf-8") as f:
        return json.load(f)


def normalize(path: str) -> str:
    while path.endswith("/") and path != "/":
        path = path[:-1]
    return path if path.startswith("/") else "/" + path


def load_baseline(path: str) -> set[str]:
    routes = set()
    with open(path, encoding="utf-8", errors="replace") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            parts = line.split(None, 1)
            if len(parts) != 2:
                continue
            routes.add((parts[0].upper(), normalize(parts[1])))
    return routes


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--openapi", required=True, help="URL atau path file OpenAPI JSON")
    parser.add_argument("--baseline", required=True, help="Path file route-baseline.txt")
    args = parser.parse_args()

    spec = fetch_openapi(args.openapi)
    live = set()
    for path, methods in spec.get("paths", {}).items():
        for method in methods:
            if method.lower() in {"get", "post", "put", "patch", "delete"}:
                live.add((method.upper(), normalize(path)))

    baseline = load_baseline(args.baseline)

    missing = sorted(baseline - live)
    extra = sorted(live - baseline)

    print(f"LIVE: {len(live)} | BASELINE: {len(baseline)}")
    print(f"MISSING ({len(missing)}): {missing}")
    print(f"EXTRA ({len(extra)}): {extra}")

    if missing or extra:
        print("PARITY: FAIL")
        return 1
    print("PARITY: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())