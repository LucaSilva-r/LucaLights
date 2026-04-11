<script lang="ts">
	import {
		clamp,
		hexToRgb,
		hsvToRgb,
		normalizeHex,
		rgbToHex,
		rgbToHsv,
		type HsvColor,
		type RgbColor
	} from './color-utils';

	const DEFAULT_HEX = '#ffffff';

	let {
		hex = DEFAULT_HEX,
		onchange
	}: {
		hex?: string;
		onchange?: (hex: string) => void;
	} = $props();

	function getSafeHex(value: string) {
		return normalizeHex(value) ?? DEFAULT_HEX;
	}

	function getSafeRgb(value: string) {
		return hexToRgb(getSafeHex(value)) ?? { r: 255, g: 255, b: 255 };
	}

	let hsv = $state<HsvColor>(rgbToHsv(getSafeRgb(DEFAULT_HEX)));
	let hexInput = $state(DEFAULT_HEX);

	let previewRgb = $derived(hsvToRgb(hsv));
	let previewHex = $derived(rgbToHex(previewRgb));
	let hueHex = $derived(rgbToHex(hsvToRgb({ h: hsv.h, s: 1, v: 1 })));
	let saturationX = $derived(`${hsv.s * 100}%`);
	let valueY = $derived(`${(1 - hsv.v) * 100}%`);
	let hueX = $derived(`${(hsv.h / 360) * 100}%`);

	$effect(() => {
		const normalized = getSafeHex(hex);
		if (normalized === previewHex && normalized === hexInput) {
			return;
		}

		hsv = rgbToHsv(getSafeRgb(normalized));
		hexInput = normalized;
	});

	function commitHsv(next: HsvColor) {
		hsv = {
			h: ((next.h % 360) + 360) % 360,
			s: clamp(next.s, 0, 1),
			v: clamp(next.v, 0, 1)
		};

		const nextHex = rgbToHex(hsvToRgb(hsv));
		hexInput = nextHex;
		onchange?.(nextHex);
	}

	function commitRgb(next: RgbColor) {
		const safe = {
			r: clamp(Math.round(next.r), 0, 255),
			g: clamp(Math.round(next.g), 0, 255),
			b: clamp(Math.round(next.b), 0, 255)
		};

		hsv = rgbToHsv(safe);
		const nextHex = rgbToHex(safe);
		hexInput = nextHex;
		onchange?.(nextHex);
	}

	function handleHexInput(event: Event) {
		hexInput = (event.currentTarget as HTMLInputElement).value;
		const normalized = normalizeHex(hexInput);
		if (!normalized) {
			return;
		}

		hsv = rgbToHsv(getSafeRgb(normalized));
		hexInput = normalized;
		onchange?.(normalized);
	}

	function handleRgbInput(channel: keyof RgbColor, event: Event) {
		const value = parseInt((event.currentTarget as HTMLInputElement).value, 10);
		commitRgb({
			...previewRgb,
			[channel]: Number.isFinite(value) ? value : 0
		});
	}

	function beginSurfaceDrag(event: PointerEvent) {
		const target = event.currentTarget as HTMLElement;
		target.setPointerCapture(event.pointerId);

		function update(clientX: number, clientY: number) {
			const rect = target.getBoundingClientRect();
			const nextS = clamp((clientX - rect.left) / rect.width, 0, 1);
			const nextV = 1 - clamp((clientY - rect.top) / rect.height, 0, 1);
			commitHsv({ ...hsv, s: nextS, v: nextV });
		}

		function onMove(e: PointerEvent) {
			update(e.clientX, e.clientY);
		}

		function onUp() {
			target.removeEventListener('pointermove', onMove);
			target.removeEventListener('pointerup', onUp);
			if (target.hasPointerCapture(event.pointerId)) {
				target.releasePointerCapture(event.pointerId);
			}
		}

		update(event.clientX, event.clientY);
		target.addEventListener('pointermove', onMove);
		target.addEventListener('pointerup', onUp);
	}

	function beginHueDrag(event: PointerEvent) {
		const target = event.currentTarget as HTMLElement;
		target.setPointerCapture(event.pointerId);

		function update(clientX: number) {
			const rect = target.getBoundingClientRect();
			const nextHue = clamp((clientX - rect.left) / rect.width, 0, 1) * 360;
			commitHsv({ ...hsv, h: nextHue });
		}

		function onMove(e: PointerEvent) {
			update(e.clientX);
		}

		function onUp() {
			target.removeEventListener('pointermove', onMove);
			target.removeEventListener('pointerup', onUp);
			if (target.hasPointerCapture(event.pointerId)) {
				target.releasePointerCapture(event.pointerId);
			}
		}

		update(event.clientX);
		target.addEventListener('pointermove', onMove);
		target.addEventListener('pointerup', onUp);
	}
</script>

<div class="w-60 space-y-3 text-foreground">
	<div class="flex items-center gap-3">
		<div
			class="h-10 w-10 shrink-0 rounded-xl border border-white/10 shadow-[inset_0_1px_0_rgba(255,255,255,0.18)]"
			style="background-color: {previewHex};"
		></div>
		<div class="min-w-0 flex-1">
			<div class="text-[10px] font-semibold uppercase tracking-[0.22em] text-muted-foreground">
				Selected
			</div>
			<div class="font-mono text-[13px] tracking-[0.08em] text-foreground">{previewHex}</div>
		</div>
	</div>

	<!-- svelte-ignore a11y_no_static_element_interactions -->
	<div
		class="relative h-40 cursor-crosshair overflow-hidden rounded-xl border border-white/10 bg-black shadow-[inset_0_1px_0_rgba(255,255,255,0.04)]"
		onpointerdown={beginSurfaceDrag}
	>
		<div
			class="absolute inset-0 rounded-[inherit]"
			style="background: linear-gradient(to top, #000 0%, transparent 100%), linear-gradient(to right, #fff 0%, transparent 100%), {hueHex};"
		></div>
		<div
			class="pointer-events-none absolute h-4 w-4 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-white shadow-[0_0_0_1px_rgba(0,0,0,0.55),0_6px_16px_rgba(0,0,0,0.45)]"
			style="left: {saturationX}; top: {valueY};"
		></div>
	</div>

	<div class="space-y-1.5">
		<div class="flex items-center justify-between text-[10px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
			<span>Hue</span>
			<span class="font-mono">{Math.round(hsv.h)}°</span>
		</div>
		<!-- svelte-ignore a11y_no_static_element_interactions -->
		<div
			class="relative h-3 cursor-ew-resize rounded-full border border-white/10 shadow-[inset_0_1px_0_rgba(255,255,255,0.05)]"
			style="background: linear-gradient(to right, #ff0000 0%, #ffff00 17%, #00ff00 33%, #00ffff 50%, #0000ff 67%, #ff00ff 83%, #ff0000 100%);"
			onpointerdown={beginHueDrag}
		>
			<div
				class="pointer-events-none absolute top-1/2 h-4 w-4 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-white bg-transparent shadow-[0_0_0_1px_rgba(0,0,0,0.55),0_4px_12px_rgba(0,0,0,0.35)]"
				style="left: {hueX};"
			></div>
		</div>
	</div>

	<div class="grid grid-cols-3 gap-2">
		<label class="col-span-3 space-y-1">
			<span class="text-[10px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">Hex</span>
			<input
				class="h-9 w-full rounded-lg border border-white/10 bg-black/35 px-3 font-mono text-[13px] text-foreground outline-none transition focus:border-primary/50 focus:ring-2 focus:ring-primary/20"
				type="text"
				value={hexInput}
				inputmode="text"
				spellcheck="false"
				autocapitalize="off"
				autocomplete="off"
				oninput={handleHexInput}
			/>
		</label>

		<label class="space-y-1">
			<span class="text-[10px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">R</span>
			<input
				class="h-9 w-full rounded-lg border border-white/10 bg-black/35 px-2.5 text-center font-mono text-[13px] text-foreground outline-none transition focus:border-primary/50 focus:ring-2 focus:ring-primary/20"
				type="number"
				min="0"
				max="255"
				value={previewRgb.r}
				oninput={(event) => handleRgbInput('r', event)}
			/>
		</label>

		<label class="space-y-1">
			<span class="text-[10px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">G</span>
			<input
				class="h-9 w-full rounded-lg border border-white/10 bg-black/35 px-2.5 text-center font-mono text-[13px] text-foreground outline-none transition focus:border-primary/50 focus:ring-2 focus:ring-primary/20"
				type="number"
				min="0"
				max="255"
				value={previewRgb.g}
				oninput={(event) => handleRgbInput('g', event)}
			/>
		</label>

		<label class="space-y-1">
			<span class="text-[10px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">B</span>
			<input
				class="h-9 w-full rounded-lg border border-white/10 bg-black/35 px-2.5 text-center font-mono text-[13px] text-foreground outline-none transition focus:border-primary/50 focus:ring-2 focus:ring-primary/20"
				type="number"
				min="0"
				max="255"
				value={previewRgb.b}
				oninput={(event) => handleRgbInput('b', event)}
			/>
		</label>
	</div>
</div>
