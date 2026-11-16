mergeInto(LibraryManager.library, {
	// Caps the pixel ratio the canvas is sized with (RenderScaler.cs). Unity's framework reads
	// Module.devicePixelRatio before window.devicePixelRatio each time it sizes the canvas, so the page Poki builds
	// needs no change. A max of 0 or less puts back the ratio the page had. Touch input and the safe area are measured
	// against the canvas's own width over its CSS width, so they follow.
	JS_CanvasRatio_Limit: function (max) {
		if (Module.bonkOriginalRatio === undefined) {
			Module.bonkOriginalRatio = Module.devicePixelRatio || 0;
		}

		if (max <= 0) {
			Module.devicePixelRatio = Module.bonkOriginalRatio || undefined;
			return;
		}

		var ratio = Module.bonkOriginalRatio || window.devicePixelRatio || 1;
		Module.devicePixelRatio = Math.min(ratio, max);
	}
});
