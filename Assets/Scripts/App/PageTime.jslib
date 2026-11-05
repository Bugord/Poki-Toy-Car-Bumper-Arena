mergeInto(LibraryManager.library, {
	// Seconds since the page began to load (SessionTime.cs), so a visit's time takes in the download and the engine's
	// start, as the portal's session does.
	JS_PageTime_Seconds: function () {
		return (window.performance && performance.now) ? performance.now() / 1000 : 0;
	}
});
