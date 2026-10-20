mergeInto(LibraryManager.library, {
	// Whether the player's main pointer is a finger (HudControls.cs). The browser's user agent misses some: Safari on
	// an iPad says it is a Mac, and a phone asked for the desktop site says it is a desktop; the primary pointer gives
	// them away. A laptop with a touch screen has a mouse or a touchpad first and answers no, which is right: its
	// player starts with the mouse, and its first touch switches the game to the touch layout anyway.
	JS_TouchScreen_IsPrimary: function () {
		if (window.matchMedia) {
			var coarse = window.matchMedia('(pointer: coarse)');
			// A browser that does not know the query answers it as 'not all'.
			if (coarse.media !== 'not all') {
				return coarse.matches ? 1 : 0;
			}
		}

		return (navigator.maxTouchPoints || 0) > 0 ? 1 : 0;
	}
});
