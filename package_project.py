#!/usr/bin/env python3
"""Package HmaxUnlockerPro and create manifest."""

import os
import zipfile
from pathlib import Path

OUT = Path("HmaxUnlockerPro")
ZIP_PATH = Path("HmaxUnlockerPro_Complete.zip")
MANIFEST = Path("manifest.txt")


def main():
    # Create ZIP preserving HmaxUnlockerPro/ root folder
    with zipfile.ZipFile(ZIP_PATH, "w", zipfile.ZIP_DEFLATED) as zf:
        for path in sorted(OUT.rglob("*")):
            if path.is_file():
                zf.write(path, str(path))

    # Create manifest
    lines = ["HmaxUnlockerPro Project File Listing", "=" * 60, ""]
    for path in sorted(OUT.rglob("*")):
        if path.is_file():
            size = path.stat().st_size
            lines.append(f"{path.relative_to(OUT)} {size} bytes")

    # Add zip itself to manifest
    lines.append("")
    lines.append(f"{ZIP_PATH} {ZIP_PATH.stat().st_size} bytes")
    MANIFEST.write_text("\n".join(lines), encoding="utf-8")

    print(f"Created {ZIP_PATH} ({ZIP_PATH.stat().st_size} bytes)")
    print(f"Created {MANIFEST}")


if __name__ == "__main__":
    main()
