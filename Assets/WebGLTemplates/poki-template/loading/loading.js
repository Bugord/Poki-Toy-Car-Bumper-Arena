/*
 * Bonk Arena's loading screen: a crowned monster truck pushes a car along the loading bar and, once the game is ready,
 * knocks it off the end.
 *
 * Poki's loader (master-loader.js, then unity-2020.js) still does the loading, the SDK calls and the Unity bridge;
 * this only covers it. Its elements stay in the page untouched: it looks them up by id while it loads. The progress
 * is read from its bar (#progress-fill), so nothing of the loader is patched. Poki's loader goes away as soon as the
 * engine is up, but Boot and the first arena take seconds more, so this screen stays until the game calls
 * bonkLoading.hide() (LoadingScreen.cs) when the first round is on screen, with time-outs in case it never does.
 *
 * Loaded from extra_html in index.json (the page Poki generates) and from index.html (the local preview); a second
 * copy does nothing.
 */
(function () {
	'use strict';
	if (window.bonkLoading) {
		return;
	}

	var script = document.currentScript;
	var base = script && script.src ? script.src.substring(0, script.src.lastIndexOf('/') + 1) : 'loading/';

	// Share of the bar the engine's download fills; the rest creeps while the game starts and jumps on hide().
	var EngineShare = 0.88;
	var StartingShare = 0.1;
	var StartingSeconds = 2.5;
	// Seconds after the engine is up, and after the page opened, before the screen goes even without hide().
	var ReadyTimeout = 30;
	var PageTimeout = 120;
	var KnockSeconds = 0.9;
	var FadeDelay = 0.5;
	var FadeSeconds = 0.35;

	var css = [
		"@font-face{font-family:'Bonk Loading';src:url('" + base + "font.woff2') format('woff2');font-display:block}",
		'#bonk-loading{--navy:#1d2b4f;--w:min(86vw,640px,100vh);position:fixed;inset:0;z-index:10000;overflow:hidden;',
		'background:#38b6ff;display:flex;align-items:center;justify-content:center;touch-action:none;user-select:none;',
		"-webkit-user-select:none;font-family:'Bonk Loading','Arial Black',Impact,sans-serif;transition:opacity .35s}",
		'#bonk-loading.bl-gone{opacity:0;pointer-events:none}',
		'#bonk-loading .bl-checker{position:absolute;inset:-192px;animation:bl-drift 9s linear infinite;',
		'background:repeating-conic-gradient(rgba(255,255,255,.14) 0 25%,transparent 0 50%) 0 0/192px 192px}',
		'#bonk-loading .bl-column{position:relative;width:var(--w);display:flex;flex-direction:column;align-items:center}',
		'#bonk-loading .bl-text{color:#fff;-webkit-text-stroke:calc(var(--w)*.02) var(--navy);paint-order:stroke fill;',
		'text-shadow:0 calc(var(--w)*.016) 0 var(--navy);letter-spacing:.01em;white-space:nowrap}',
		'#bonk-loading .bl-logo{display:flex;flex-direction:column;align-items:center;line-height:.88;',
		'transform:rotate(-4deg);animation:bl-pop .5s cubic-bezier(.3,1.6,.5,1) both}',
		'#bonk-loading .bl-bonk{font-size:calc(var(--w)*.25);color:#ffc93c}',
		'#bonk-loading .bl-arena{font-size:calc(var(--w)*.13);-webkit-text-stroke-width:calc(var(--w)*.016)}',
		'#bonk-loading .bl-stage{position:relative;width:100%;margin-top:calc(var(--w)*.04)}',
		'#bonk-loading .bl-lane{position:relative;height:calc(var(--w)*.34)}',
		'#bonk-loading .bl-ride{position:absolute;left:0;bottom:calc(var(--w)*-.012);will-change:transform}',
		'#bonk-loading .bl-ride img{display:block;width:100%;height:auto}',
		'#bonk-loading .bl-truck,#bonk-loading .bl-car{width:calc(var(--w)*.3)}',
		'#bonk-loading .bl-truck .bl-body{animation:bl-bob .24s ease-in-out infinite alternate}',
		'#bonk-loading .bl-car .bl-body{transform-origin:85% 100%;animation:bl-wobble .32s ease-in-out infinite alternate}',
		'#bonk-loading .bl-puff{position:absolute;left:0;bottom:2%;width:14%;aspect-ratio:1;border-radius:50%;',
		'background:#fff;border:calc(var(--w)*.006) solid var(--navy);animation:bl-puff .6s linear infinite}',
		'#bonk-loading .bl-puff:nth-child(2){animation-delay:-.2s}#bonk-loading .bl-puff:nth-child(3){animation-delay:-.4s}',
		'#bonk-loading .bl-spark{position:absolute;left:0;bottom:12%;width:calc(var(--w)*.07);margin-left:calc(var(--w)*-.035);',
		'will-change:transform}#bonk-loading .bl-spark svg{display:block;width:100%;animation:bl-spark .3s ease-in-out infinite alternate}',
		'#bonk-loading .bl-track{position:relative;height:calc(var(--w)*.055);border-radius:999px;background:#fff;',
		'border:calc(var(--w)*.012) solid var(--navy);box-shadow:0 calc(var(--w)*.014) 0 var(--navy);overflow:hidden}',
		'#bonk-loading .bl-fill{position:absolute;left:0;top:0;bottom:0;width:0;border-radius:999px;background:#5bd34a;',
		'box-shadow:inset 0 calc(var(--w)*.012) 0 rgba(255,255,255,.4)}',
		'#bonk-loading .bl-percent{margin-top:calc(var(--w)*.03);font-size:calc(var(--w)*.065);',
		'-webkit-text-stroke-width:calc(var(--w)*.012);text-shadow:0 calc(var(--w)*.01) 0 var(--navy)}',
		'#bonk-loading .bl-bang{position:absolute;right:calc(var(--w)*-.06);bottom:calc(var(--w)*.25);',
		'font-size:calc(var(--w)*.12);color:#ff4d4d;transform:scale(0) rotate(-10deg)}',
		'#bonk-loading .bl-bang.bl-on{animation:bl-bang .45s cubic-bezier(.3,1.7,.5,1) forwards}',
		'@keyframes bl-drift{to{transform:translateX(192px)}}',
		'@keyframes bl-pop{from{transform:rotate(-4deg) scale(.4);opacity:0}to{transform:rotate(-4deg) scale(1);opacity:1}}',
		'@keyframes bl-bob{from{transform:translateY(0)}to{transform:translateY(calc(var(--w)*-.006))}}',
		'@keyframes bl-wobble{from{transform:rotate(-2deg)}to{transform:rotate(3deg)}}',
		'@keyframes bl-puff{from{transform:translate(0,0) scale(.9)}to{transform:translate(-160%,-60%) scale(0)}}',
		'@keyframes bl-spark{from{transform:scale(.7) rotate(0)}to{transform:scale(1.1) rotate(20deg)}}',
		'@keyframes bl-bang{0%{transform:scale(0) rotate(-30deg)}100%{transform:scale(1) rotate(-10deg)}}'
	].join('');

	// A five-pointed star, the KO star of the game.
	var star = '<svg viewBox="-12 -12 24 24"><path d="M0-10 2.9-3.9 9.5-3.1 4.6 1.5 5.9 8.1 0 4.8-5.9 8.1-4.6 1.5-9.5-3.1-2.9-3.9Z" ' +
		'fill="#ffc93c" stroke="#1d2b4f" stroke-width="2.2" stroke-linejoin="round"/></svg>';

	var html =
		'<div class="bl-checker"></div>' +
		'<div class="bl-column">' +
			'<div class="bl-logo"><span class="bl-text bl-bonk">BONK</span><span class="bl-text bl-arena">ARENA</span></div>' +
			'<div class="bl-stage">' +
				'<div class="bl-lane">' +
					'<div class="bl-ride bl-truck"><span class="bl-puff"></span><span class="bl-puff"></span><span class="bl-puff"></span>' +
						'<div class="bl-body"><img alt="" src="' + base + 'truck.webp"></div></div>' +
					'<div class="bl-ride bl-car"><div class="bl-body"><img alt="" src="' + base + 'car.webp"></div></div>' +
					'<div class="bl-spark">' + star + '</div>' +
					'<div class="bl-text bl-bang">BONK!</div>' +
				'</div>' +
				'<div class="bl-track"><div class="bl-fill"></div></div>' +
			'</div>' +
			'<div class="bl-text bl-percent">0%</div>' +
		'</div>';

	var root, truck, car, spark, fill, track, percent, bang;
	var shown = 0;
	var engineAt = 0;
	var finishing = false;
	var knockAt = 0;
	var shownPercent = -1;
	var last = 0;
	var done = false;

	function mount() {
		var style = document.createElement('style');
		style.textContent = css;
		document.head.appendChild(style);
		root = document.createElement('div');
		root.id = 'bonk-loading';
		root.innerHTML = html;
		document.body.appendChild(root);
		truck = root.querySelector('.bl-truck');
		car = root.querySelector('.bl-car');
		spark = root.querySelector('.bl-spark');
		fill = root.querySelector('.bl-fill');
		track = root.querySelector('.bl-track');
		percent = root.querySelector('.bl-percent');
		bang = root.querySelector('.bl-bang');
		var images = root.querySelectorAll('img');
		for (var i = 0; i < images.length; i++) {
			images[i].onerror = function () { this.style.visibility = 'hidden'; };
		}

		setTimeout(hide, PageTimeout * 1000);
		requestAnimationFrame(frame);
	}

	// The engine's download as Poki's loader shows it, 0..1.
	function engineProgress() {
		var bar = document.getElementById('progress-fill');
		var width = bar ? parseFloat(bar.style.width) : NaN;
		return isNaN(width) ? 0 : Math.min(1, Math.max(0, width / 100));
	}

	// Poki's loader hides itself and shows the game once the engine has started.
	function isEngineUp() {
		var loader = document.getElementById('loader');
		var game = document.getElementById('game-container');
		return (loader && loader.style.display === 'none') || (game && game.style.display && game.style.display !== 'none');
	}

	function frame(now) {
		var seconds = now / 1000;
		var dt = last ? Math.min(0.1, seconds - last) : 0;
		last = seconds;

		var target;
		if (finishing) {
			target = 1;
		}
		else if (engineAt || isEngineUp()) {
			if (!engineAt) {
				engineAt = seconds;
				setTimeout(hide, ReadyTimeout * 1000);
			}
			target = EngineShare + StartingShare * (1 - Math.exp(-(seconds - engineAt) / StartingSeconds));
		}
		else {
			target = EngineShare * engineProgress();
		}

		shown += (target - shown) * (1 - Math.exp(-dt * (finishing ? 14 : 5)));
		if (target - shown < 0.002) {
			shown = target;
		}

		// The car goes off only once the bar is full.
		if (finishing && !knockAt && shown >= 1) {
			knock(seconds);
		}

		place(shown, knockAt ? seconds - knockAt : -1);
		if (!done) {
			requestAnimationFrame(frame);
		}
	}

	// The truck pushes the car from the bar's start until the car hangs half over its end; the green is the progress.
	function place(progress, knocked) {
		var length = track.clientWidth;
		var truckWidth = truck.offsetWidth;
		var carWidth = car.offsetWidth;
		var truckX = progress * (length - truckWidth - carWidth * 0.5);
		var carX = truckX + truckWidth * 0.93;
		var carY = 0;
		var spin = 0;

		if (knocked >= 0) {
			// The truck lunges and the car flies off the end, tumbling backwards, and falls past the bottom.
			var lunge = Math.min(1, knocked / 0.12);
			truckX += truckWidth * 0.12 * Math.sin(lunge * Math.PI * 0.5);
			var t = knocked;
			carX += carWidth * 2.4 * t;
			carY = carWidth * (3.2 * t - 5 * t * t);
			spin = 620 * t;
			spark.style.display = 'none';
		}

		truck.style.transform = 'translateX(' + truckX + 'px)';
		car.style.transform = 'translate(' + carX + 'px,' + (-carY) + 'px) rotate(' + spin + 'deg)';
		spark.style.transform = 'translateX(' + (truckX + truckWidth * 0.96) + 'px)';
		fill.style.width = progress * length + 'px';

		var whole = Math.floor(progress * 100 + 0.0001);
		if (whole !== shownPercent) {
			shownPercent = whole;
			percent.textContent = whole + '%';
		}
	}

	// Input goes to the game at once; the bar fills, the car is knocked off over the game and the screen fades.
	function hide() {
		if (!root || finishing) {
			return;
		}

		finishing = true;
		root.style.pointerEvents = 'none';
	}

	function knock(seconds) {
		knockAt = seconds;
		bang.classList.add('bl-on');
		setTimeout(function () { root.classList.add('bl-gone'); }, FadeDelay * 1000);
		setTimeout(function () {
			done = true;
			if (root.parentNode) {
				root.parentNode.removeChild(root);
			}
		}, Math.max(KnockSeconds, FadeDelay + FadeSeconds) * 1000 + 50);
	}

	window.bonkLoading = { hide: hide };
	if (document.body) {
		mount();
	}
	else {
		document.addEventListener('DOMContentLoaded', mount);
	}
})();
