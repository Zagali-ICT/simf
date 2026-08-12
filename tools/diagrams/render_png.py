"""Render a diagram sheet SVG to PNG with headless Chrome.

Usage:  python tools/diagrams/render_png.py SIMF-Fig7-Layered-Architecture

Two traps this wraps, both learned the hard way:
  * the --screenshot path must be absolute, or Chrome fails with access denied
  * plain --headless only; --headless=old exits silently writing no file
A fresh user-data-dir stops Chrome serving a cached copy of the file:// URL.
"""
import os
import re
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
DIAGRAMS = os.path.join(os.path.dirname(os.path.dirname(HERE)), "docs", "diagrams")
CHROME_CANDIDATES = [
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
]
SCALE = 2


def chrome():
    for path in CHROME_CANDIDATES:
        if os.path.exists(path):
            return path
    raise SystemExit("Chrome was not found in either standard location.")


def render(stem):
    svg = os.path.join(DIAGRAMS, stem + ".svg")
    png = os.path.join(DIAGRAMS, stem + ".png")
    head = open(svg, encoding="utf-8").read(400)
    w = int(float(re.search(r'width="([\d.]+)"', head).group(1)))
    h = int(float(re.search(r'height="([\d.]+)"', head).group(1)))
    profile = tempfile.mkdtemp(prefix="simfdiag_")
    try:
        subprocess.run([
            chrome(), "--headless", "--disable-gpu", "--no-sandbox",
            "--hide-scrollbars", f"--user-data-dir={profile}",
            f"--force-device-scale-factor={SCALE}",
            f"--window-size={w},{h}",
            f"--screenshot={png}",
            "file:///" + svg.replace("\\", "/"),
        ], check=True, capture_output=True, timeout=180)
    finally:
        shutil.rmtree(profile, ignore_errors=True)
    if not os.path.exists(png):
        raise SystemExit(f"no PNG written for {stem}")
    print(f"wrote {png}  ({w}x{h} at scale {SCALE})")


if __name__ == "__main__":
    targets = sys.argv[1:]
    if not targets:
        targets = [os.path.splitext(f)[0] for f in sorted(os.listdir(DIAGRAMS))
                   if f.startswith("SIMF-Fig") and f.endswith(".svg")]
    for stem in targets:
        render(stem)
