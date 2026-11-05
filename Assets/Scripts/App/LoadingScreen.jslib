mergeInto(LibraryManager.library, {
	// The template's loading screen (loading/loading.js); a page without it, such as an older template, ignores the call.
	JS_LoadingScreen_Hide: function () {
		if (window.bonkLoading && window.bonkLoading.hide) {
			window.bonkLoading.hide();
		}
	}
});
