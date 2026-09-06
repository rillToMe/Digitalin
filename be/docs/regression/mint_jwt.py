#!/usr/bin/env python3
"""Mint JWT HS256 untuk smoke test (dev key dari appsettings.Development.json).

Usage:
    python docs/regression/mint_jwt.py --role ADMIN [--name admin]
"""
import argparse
import base64
import hashlib
import hmac
import json
import time

ISSUER = "DigitalinBackend"
AUDIENCE = "DigitalinAdmin"
KEY = "DigitalinSuperRahasiaJWTKeyYangSangatPanjang123!!"


def b64url(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--role", required=True, choices=["ADMIN", "SUPER_ADMIN"])
    parser.add_argument("--name", default="admin")
    args = parser.parse_args()

    now = int(time.time())
    header = {"alg": "HS256", "typ": "JWT"}
    payload = {
        "nameid": "1",
        "unique_name": args.name,
        "role": args.role,
        "iss": ISSUER,
        "aud": AUDIENCE,
        "nbf": now - 60,
        "exp": now + 12 * 3600,
    }
    signing_input = f"{b64url(json.dumps(header).encode())}.{b64url(json.dumps(payload).encode())}"
    sig = b64url(hmac.new(KEY.encode(), signing_input.encode(), hashlib.sha256).digest())
    print(f"{signing_input}.{sig}")


if __name__ == "__main__":
    main()