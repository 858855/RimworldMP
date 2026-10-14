#!/usr/bin/env python3
"""Verify the supplied knowledge ZIP against its embedded SHA-256 manifest."""
from hashlib import sha256
from pathlib import Path
from zipfile import ZipFile

repository = Path(__file__).resolve().parents[1]
archive = repository / "RimWorld-Codex-Knowledge.zip"
prefix = "RimWorld-Codex-Knowledge/"
with ZipFile(archive) as source:
    manifest = source.read(prefix + "MANIFEST.sha256").decode("utf-8")
    checked = 0
    for line in manifest.splitlines():
        digest, relative = line.split("  ", 1)
        actual = sha256(source.read(prefix + relative)).hexdigest()
        if actual != digest:
            raise SystemExit(f"FAIL: {relative}: SHA-256 mismatch")
        checked += 1
print(f"PASS: {checked} archive entries match the embedded manifest.")
