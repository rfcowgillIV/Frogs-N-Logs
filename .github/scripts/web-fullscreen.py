"""Tweaks Unity's web page so Frogs-N-Logs fills any screen (added 2026).

Unity's default page draws the game at a fixed 960x600 unless it recognizes a phone, and iPads
report themselves as Macs, so on an upright iPad (or a narrow window) the right side was cut off.
This switches the page to Unity's own "fill the window" mode on every device, stops finger drags
from scrolling or zooming the page, and gives the tab a friendlier title.
Usage: python3 web-fullscreen.py path/to/index.html
"""
import sys

path = sys.argv[1]
html = open(path, encoding="utf-8").read()

def swap(old, new):
    global html
    if old not in html:
        sys.exit("web-fullscreen: couldn't find expected text in index.html: " + old[:60])
    html = html.replace(old, new, 1)

swap('if (/iPhone|iPad|iPod|Android/i.test(navigator.userAgent)) {',
     'if (true) { // Frogs-N-Logs: always fill the window (iPads report as Macs)')
swap('<title>Unity Web Player | Frogs-N-Logs</title>', '<title>Frogs-N-Logs</title>')
swap('</head>', '''  <style>
      /* Frogs-N-Logs: no page scrolling, bouncing or pinch-zoom while steering with a finger */
      html, body { height: 100%; overflow: hidden; overscroll-behavior: none; background: #231F20; }
      #unity-canvas { touch-action: none; -webkit-user-select: none; user-select: none; }
    </style>
  </head>''')
open(path, "w", encoding="utf-8").write(html)
print("web-fullscreen: index.html updated")
