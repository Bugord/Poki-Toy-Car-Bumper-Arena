mergeInto(LibraryManager.library, {
	// Writes the safe area insets (SafeArea.cs) into a float[4]: left, top, right, bottom, in canvas pixels. The
	// page's env(safe-area-inset-*) are measured from the viewport, so only the part of them over the canvas counts.
	// Inside an iframe (Poki) the browser reports them as 0, and a phone that shows the game full screen puts its
	// camera over the top of it; so a phone held upright always keeps at least the top 40 of the screen's pixels clear
	// (CSS pixels of the device, not of a page laid out wider than the screen), about the height of a status bar, which
	// a camera cutout fits in. It costs the HUD a little room where nothing is there.
	JS_SafeArea_GetInsets: function (insets) {
		var index = insets >> 2;
		for (var i = 0; i < 4; i++) {
			HEAPF32[index + i] = 0;
		}

		var canvas = Module.canvas;
		var isPhone = (navigator.maxTouchPoints || 0) > 0 && Math.min(screen.width, screen.height) < 600;
		if (!isPhone || !canvas || !document.body) {
			return;
		}

		var probe = window.bonkSafeAreaProbe;
		if (!probe) {
			probe = document.createElement('div');
			probe.style.cssText = 'position:fixed;left:0;top:0;width:0;height:0;visibility:hidden;pointer-events:none;' +
				'padding:env(safe-area-inset-top) env(safe-area-inset-right) env(safe-area-inset-bottom) env(safe-area-inset-left);';
			document.body.appendChild(probe);
			window.bonkSafeAreaProbe = probe;
		}

		var rect = canvas.getBoundingClientRect();
		if (rect.width <= 0 || rect.height <= 0) {
			return;
		}

		var style = window.getComputedStyle(probe);
		var viewportWidth = document.documentElement.clientWidth || window.innerWidth;
		var viewportHeight = document.documentElement.clientHeight || window.innerHeight;
		var scaleX = canvas.width / rect.width;
		var scaleY = canvas.height / rect.height;
		var top = parseFloat(style.paddingTop) || 0;
		if (viewportHeight > viewportWidth) {
			top = Math.max(top, 40 * Math.max(1, viewportWidth / screen.width));
		}

		HEAPF32[index] = Math.max(0, (parseFloat(style.paddingLeft) || 0) - rect.left) * scaleX;
		HEAPF32[index + 1] = Math.max(0, top - rect.top) * scaleY;
		HEAPF32[index + 2] = Math.max(0, (parseFloat(style.paddingRight) || 0) - (viewportWidth - rect.right)) * scaleX;
		HEAPF32[index + 3] = Math.max(0, (parseFloat(style.paddingBottom) || 0) - (viewportHeight - rect.bottom)) * scaleY;
	}
});
