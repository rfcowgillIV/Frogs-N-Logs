// FrogsWeb.jslib (added Nov 2026): tiny browser helpers for the web version.
// Unity reads Module.devicePixelRatio when sizing the game canvas, so lowering it makes
// Retina screens draw fewer pixels (much faster on iPads) without changing the layout.
mergeInto(LibraryManager.library, {
  FrogsSetPixelRatio: function (ratio) {
    if (ratio > 0) { Module.devicePixelRatio = ratio; }
  },
  FrogsGetDevicePixelRatio: function () {
    return window.devicePixelRatio || 1;
  }
});
