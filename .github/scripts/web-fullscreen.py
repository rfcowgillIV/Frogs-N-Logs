"""Tweaks Unity's web page so Frogs-N-Logs fills any screen (added 2026).

Unity's default page draws the game at a fixed 960x600 unless it recognizes a phone, and iPads
report themselves as Macs, so on an upright iPad (or a narrow window) the right side was cut off.
This switches the page to Unity's own "fill the window" mode on every device, stops finger drags
from scrolling or zooming the page, and gives the tab a friendlier title.

Nov 2026 (speed): Retina screens (iPads, newer Macs and phones) report 2-3 screen pixels per
CSS pixel, so the game was drawing 4-9x as many pixels as it needed to. The page now starts at
no more than 1.25x; the game's Graphics setting can raise or lower it (see FrogsWeb.jslib).
It also gets a Frogs-N-Logs loading screen instead of the Unity logo.
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
swap('// config.devicePixelRatio = 1;',
     'config.devicePixelRatio = Math.min(window.devicePixelRatio || 1, 1.25); // Frogs-N-Logs: fewer pixels, more frogs')
swap('<div id="unity-logo"></div>',
     '<div id="frogs-loading-title">Frogs-N-Logs</div><div id="frogs-loading-tip">Loading the pond&hellip; (about 20 seconds the first time, instant after that)</div>')
swap('</style>', """  /* Frogs-N-Logs loading screen */
      #unity-loading-bar { text-align: center; width: min(90vw, 520px); }
      #frogs-loading-title { font: 700 44px/1.1 "Varela Round", "Trebuchet MS", Arial, sans-serif; color: #8BC34A; margin-bottom: 10px; }
      #frogs-loading-tip { font: 15px/1.4 "Trebuchet MS", Arial, sans-serif; color: #cfd8c8; margin-bottom: 18px; }
      #unity-progress-bar-empty { width: 100%; height: 14px; margin: 0 auto; background: #3a4a33; border-radius: 7px; overflow: hidden; }
      #unity-progress-bar-full { height: 100%; background: #8BC34A; border-radius: 7px; }
    </style>""")
open(path, "w", encoding="utf-8").write(html)
print("web-fullscreen: index.html updated")
