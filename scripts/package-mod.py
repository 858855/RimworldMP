#!/usr/bin/env python3
"""Package the built RimWorld 1.6 mod without proprietary game assemblies."""
from __future__ import annotations

from hashlib import sha256
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
from zipfile import ZIP_DEFLATED, ZipFile, ZipInfo

ROOT = Path(__file__).resolve().parents[1]
MOD = ROOT / "dist" / "RimWorldMultiplayerFoundation"
OUTPUT = ROOT / "releases" / "RimWorldMultiplayerFoundation-0.1.0-dev-rw1.6.zip"
PREFIX = "RimWorldMultiplayerFoundation/"
FILES = (
    "About/About.xml",
    "Assemblies/Multiplayer.Foundation.Protocol.dll",
    "Assemblies/Multiplayer.Foundation.Network.dll",
    "Assemblies/Multiplayer.Foundation.RimWorld.dll",
)
INSTALL = ROOT / "mod" / "INSTALLATION.md"


def main() -> int:
    missing = [name for name in FILES if not (MOD / name).is_file()]
    if not INSTALL.is_file():
        missing.append(str(INSTALL.relative_to(ROOT)))
    if missing:
        print("Missing built mod files: " + ", ".join(missing), file=sys.stderr)
        return 1

    contents = {PREFIX + name: (MOD / name).read_bytes() for name in FILES}
    contents[PREFIX + "INSTALLATION.md"] = INSTALL.read_bytes()
    about = ET.fromstring(contents[PREFIX + "About/About.xml"])
    if about.findtext("packageId") != "rimworldmp.foundation":
        raise ValueError("Unexpected packageId in About.xml")
    if [node.text for node in about.findall("./supportedVersions/li")] != ["1.6"]:
        raise ValueError("Package must target only RimWorld 1.6")
    for name, data in contents.items():
        if name.endswith(".dll") and not data.startswith(b"MZ"):
            raise ValueError("Invalid .NET assembly: " + name)

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    temporary = OUTPUT.with_suffix(".zip.tmp")
    try:
        with ZipFile(temporary, "w", compression=ZIP_DEFLATED, compresslevel=9) as archive:
            for name, data in sorted(contents.items()):
                entry = ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
                entry.compress_type = ZIP_DEFLATED
                entry.external_attr = 0o644 << 16
                archive.writestr(entry, data, compress_type=ZIP_DEFLATED, compresslevel=9)
        with ZipFile(temporary) as archive:
            if set(archive.namelist()) != set(contents):
                raise ValueError("Unexpected ZIP entries")
            if archive.testzip() is not None:
                raise ValueError("Corrupt ZIP entry")
            for name, data in contents.items():
                if archive.read(name) != data:
                    raise ValueError("ZIP content mismatch: " + name)
        temporary.replace(OUTPUT)
    finally:
        temporary.unlink(missing_ok=True)
    print(f"PASS: {OUTPUT.relative_to(ROOT)} ({OUTPUT.stat().st_size} bytes, {len(contents)} files)")
    print("SHA-256: " + sha256(OUTPUT.read_bytes()).hexdigest())
    return 0


if __name__ == "__main__":
    sys.exit(main())
