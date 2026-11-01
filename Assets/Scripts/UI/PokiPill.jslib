mergeInto(LibraryManager.library, {
	// Writes where Poki's pill covers the canvas (PokiPill.cs) into a float[4]: left, top, right, bottom, in canvas
	// pixels, and returns 1; returns 0 where Poki shows no pill. Poki shows it only on a phone (touch, the screen's
	// short side under 600 CSS px, as in SafeArea.jslib), at its default place unless the game moves it (this one does
	// not): 24 CSS px from the top of the game at its left edge, 46x62 CSS px (its size on screens under 1211 px).
	// CSS pixels of the device, not of a page laid out wider than the screen, as SafeArea.jslib counts its 40.
	JS_PokiPill_GetRect: function (rect) {
		try {
			if (typeof PokiSDK === 'undefined') {
				return 0;
			}

			var isPhone = (navigator.maxTouchPoints || 0) > 0 && Math.min(screen.width, screen.height) < 600;
			var canvas = Module.canvas;
			if (!isPhone || !canvas) {
				return 0;
			}

			var bounds = canvas.getBoundingClientRect();
			if (bounds.width <= 0 || bounds.height <= 0) {
				return 0;
			}

			var viewportWidth = document.documentElement.clientWidth || window.innerWidth;
			var css = Math.max(1, viewportWidth / screen.width);
			var scaleX = canvas.width / bounds.width;
			var scaleY = canvas.height / bounds.height;
			var index = rect >> 2;
			HEAPF32[index] = (0 - bounds.left) * scaleX;
			HEAPF32[index + 1] = (24 * css - bounds.top) * scaleY;
			HEAPF32[index + 2] = (46 * css - bounds.left) * scaleX;
			HEAPF32[index + 3] = ((24 + 62) * css - bounds.top) * scaleY;
			return 1;
		} catch (e) {
			return 0;
		}
	}
});
