<script lang="ts">
	import { Plus, Minus } from '@lucide/svelte';
	import ColorPickerPanel from './ColorPickerPanel.svelte';
	import NodeNumberInput from './NodeNumberInput.svelte';
	import { rgbToHex } from './color-utils';

	export interface GradientStop {
		p: number;
		r: number;
		g: number;
		b: number;
	}

	let {
		stops = [
			{ p: 0, r: 0, g: 0, b: 0 },
			{ p: 1, r: 255, g: 255, b: 255 }
		],
		interpolation = 'linear',
		onchange
	}: {
		stops?: GradientStop[];
		interpolation?: string;
		onchange?: (stops: GradientStop[]) => void;
	} = $props();

	// Track selection by original array index for stable identity during drags
	let selectedOriginalIndex = $state(0);
	let colorPickerOpen = $state(false);
	let swatchEl: HTMLButtonElement | undefined = $state();
	let popoverEl: HTMLDivElement | undefined = $state();

	// Sorted view preserving original indices
	let sortedView = $derived.by(() => {
		return stops
			.map((stop, originalIndex) => ({ stop, originalIndex }))
			.sort((a, b) => a.stop.p - b.stop.p);
	});

	let selectedSortedIndex = $derived(
		sortedView.findIndex((entry) => entry.originalIndex === selectedOriginalIndex)
	);

	let selectedStop = $derived(
		stops[selectedOriginalIndex] ?? stops[0]
	);

	let selectedHex = $derived(
		selectedStop ? rgbToHex(selectedStop) : '#000000'
	);

	let gradientCss = $derived.by(() => {
		if (sortedView.length === 0) return 'linear-gradient(to right, #000 0%, #fff 100%)';
		if (sortedView.length === 1) {
			const c = rgbToHex(sortedView[0].stop);
			return `linear-gradient(to right, ${c} 0%, ${c} 100%)`;
		}

		const first = sortedView[0].stop;
		const last = sortedView[sortedView.length - 1].stop;
		const firstColor = rgbToHex(first);
		const lastColor = rgbToHex(last);

		if (interpolation === 'constant') {
			const parts: string[] = [];
			// Pin start at 0%
			if (first.p > 0) parts.push(`${firstColor} 0%`);
			for (let i = 0; i < sortedView.length; i++) {
				const curr = sortedView[i].stop;
				const color = rgbToHex(curr);
				const pct = `${(curr.p * 100).toFixed(1)}%`;
				parts.push(`${color} ${pct}`);
				if (i < sortedView.length - 1) {
					const nextPct = `${(sortedView[i + 1].stop.p * 100).toFixed(1)}%`;
					parts.push(`${color} ${nextPct}`);
				}
			}
			// Pin end at 100%
			if (last.p < 1) parts.push(`${lastColor} 100%`);
			return `linear-gradient(to right, ${parts.join(', ')})`;
		}

		// Linear: explicitly pin 0% and 100% so no browser extrapolation artifacts
		const parts: string[] = [];
		if (first.p > 0) parts.push(`${firstColor} 0%`);
		for (const { stop: s } of sortedView) {
			parts.push(`${rgbToHex(s)} ${(s.p * 100).toFixed(1)}%`);
		}
		if (last.p < 1) parts.push(`${lastColor} 100%`);
		return `linear-gradient(to right, ${parts.join(', ')})`;
	});

	// Close on click outside
	$effect(() => {
		if (!colorPickerOpen) return;
		function handleClickOutside(event: PointerEvent) {
			const target = event.target as HTMLElement;
			if (swatchEl?.contains(target)) return;
			if (popoverEl?.contains(target)) return;
			colorPickerOpen = false;
		}
		document.addEventListener('pointerdown', handleClickOutside, true);
		return () => document.removeEventListener('pointerdown', handleClickOutside, true);
	});

	function sampleGradient(position: number) {
		const ordered = sortedView.map(({ stop }) => stop);
		if (ordered.length === 0) {
			return { r: 128, g: 128, b: 128 };
		}

		if (ordered.length === 1) {
			return ordered[0];
		}

		const clamped = Math.max(ordered[0].p, Math.min(ordered[ordered.length - 1].p, position));
		if (clamped <= ordered[0].p) {
			return ordered[0];
		}

		if (clamped >= ordered[ordered.length - 1].p) {
			return ordered[ordered.length - 1];
		}

		for (let i = 0; i < ordered.length - 1; i++) {
			const a = ordered[i];
			const b = ordered[i + 1];
			if (clamped < a.p || clamped > b.p) {
				continue;
			}

			if (interpolation === 'constant') {
				return a;
			}

			const range = b.p - a.p;
			const t = range === 0 ? 0 : (clamped - a.p) / range;
			return {
				p: position,
				r: Math.round(a.r + (b.r - a.r) * t),
				g: Math.round(a.g + (b.g - a.g) * t),
				b: Math.round(a.b + (b.b - a.b) * t)
			};
		}

		return ordered[ordered.length - 1];
	}

	function nextStopPosition() {
		if (sortedView.length === 0) {
			return 0.5;
		}

		let bestStart = 0;
		let bestEnd = sortedView[0].stop.p;

		for (let i = 0; i < sortedView.length - 1; i++) {
			const start = sortedView[i].stop.p;
			const end = sortedView[i + 1].stop.p;
			if (end - start > bestEnd - bestStart) {
				bestStart = start;
				bestEnd = end;
			}
		}

		const last = sortedView[sortedView.length - 1].stop.p;
		if (1 - last > bestEnd - bestStart) {
			bestStart = last;
			bestEnd = 1;
		}

		return Math.round(((bestStart + bestEnd) / 2) * 100) / 100;
	}

	function emit(next: GradientStop[]) {
		onchange?.(next);
	}

	function addStop() {
		const pos = nextStopPosition();
		const sample = sampleGradient(pos);
		const next = [...stops, { p: pos, r: sample.r, g: sample.g, b: sample.b }];
		selectedOriginalIndex = next.length - 1;
		emit(next);
	}

	function removeStop() {
		if (stops.length <= 2) return;
		const next = stops.filter((_, i) => i !== selectedOriginalIndex);
		selectedOriginalIndex = Math.min(selectedOriginalIndex, next.length - 1);
		colorPickerOpen = false;
		emit(next);
	}

	function distributeStopsEvenly() {
		if (stops.length <= 1) return;

		const positions = sortedView.map((_, index) =>
			stops.length === 1 ? 0 : index / (stops.length - 1)
		);
		const next = [...stops];

		for (let i = 0; i < sortedView.length; i++) {
			const originalIndex = sortedView[i].originalIndex;
			next[originalIndex] = {
				...next[originalIndex],
				p: Math.round(positions[i] * 1000) / 1000
			};
		}

		emit(next);
	}

	function selectByOriginalIndex(originalIndex: number) {
		selectedOriginalIndex = originalIndex;
		colorPickerOpen = false;
	}

	// Drag by original index — immune to sort order changes
	function handleStopDrag(event: PointerEvent, originalIndex: number) {
		// Handles container is parent, gradient bar is its previous sibling
		const handlesContainer = (event.currentTarget as HTMLElement).parentElement!;
		const gradientBar = handlesContainer.previousElementSibling as HTMLElement;
		handlesContainer.setPointerCapture(event.pointerId);
		selectedOriginalIndex = originalIndex;

		function update(clientX: number) {
			const rect = gradientBar.getBoundingClientRect();
			const ratio = Math.max(0, Math.min(1, (clientX - rect.left) / rect.width));
			const pos = Math.round(ratio * 100) / 100;
			const next = stops.map((s, i) =>
				i === originalIndex ? { ...s, p: pos } : s
			);
			emit(next);
		}

		function onMove(e: PointerEvent) {
			update(e.clientX);
		}

		function onUp() {
			handlesContainer.removeEventListener('pointermove', onMove);
			handlesContainer.removeEventListener('pointerup', onUp);
		}

		handlesContainer.addEventListener('pointermove', onMove);
		handlesContainer.addEventListener('pointerup', onUp);
	}

	function handleColorInput(nextHex: string) {
		const hex = nextHex.replace('#', '');
		if (hex.length !== 6) return;
		const r = parseInt(hex.slice(0, 2), 16);
		const g = parseInt(hex.slice(2, 4), 16);
		const b = parseInt(hex.slice(4, 6), 16);

		const next = stops.map((s, i) =>
			i === selectedOriginalIndex ? { ...s, r, g, b } : s
		);
		emit(next);
	}

	function handlePosChange(value: number) {
		const pos = Math.max(0, Math.min(1, value));
		const next = stops.map((s, i) =>
			i === selectedOriginalIndex ? { ...s, p: Math.round(pos * 100) / 100 } : s
		);
		emit(next);
	}

	function toggleColorPicker() {
		colorPickerOpen = !colorPickerOpen;
	}
</script>

<div class="nodrag nopan space-y-2 overflow-visible">
	<!-- Toolbar: stop utilities -->
	<div class="flex items-center gap-1.5">
		<button
			type="button"
			class="flex h-6 w-6 items-center justify-center rounded-md border border-border/70 bg-background/90 text-muted-foreground shadow-sm transition hover:bg-surface-subtle-hover hover:text-foreground"
			onclick={addStop}
			title="Add stop"
		>
			<Plus class="size-3.5" />
		</button>
		<button
			type="button"
			class="flex h-6 w-6 items-center justify-center rounded-md border border-border/70 bg-background/90 text-muted-foreground shadow-sm transition hover:bg-surface-subtle-hover hover:text-foreground disabled:opacity-40"
			onclick={removeStop}
			disabled={stops.length <= 2}
			title="Remove selected stop"
		>
			<Minus class="size-3.5" />
		</button>
		<button
			type="button"
			class="flex h-6 items-center justify-center rounded-md border border-border/70 bg-background/90 px-2 text-[10px] font-medium text-muted-foreground shadow-sm transition hover:bg-surface-subtle-hover hover:text-foreground"
			onclick={distributeStopsEvenly}
			title="Distribute stops evenly"
		>
			Even
		</button>
	</div>

	<!-- Gradient bar (clipped) + stop handles below -->
	<div class="relative">
		<div
			class="h-5 overflow-hidden shadow-sm"
			style="background-image: {gradientCss};"
		></div>
		<div class="relative h-3">
			{#each sortedView as { stop, originalIndex }, sortedIdx}
				{@const isSelected = sortedIdx === selectedSortedIndex}
				<button
					type="button"
					class="absolute -top-1 h-4 w-3 -translate-x-1/2 cursor-ew-resize"
					style="left: {stop.p * 100}%;"
					title="Stop {sortedIdx + 1} at {(stop.p * 100).toFixed(0)}%"
					onpointerdown={(e) => handleStopDrag(e, originalIndex)}
					onclick={() => selectByOriginalIndex(originalIndex)}
				>
					<div
						class="mx-auto h-full w-2.5 rounded-sm border-2 shadow-sm transition {isSelected ? 'border-white ring-1 ring-primary' : 'border-white/70'}"
						style="background-color: {rgbToHex(stop)};"
					></div>
				</button>
			{/each}
		</div>
	</div>

	<!-- Selected stop info -->
	{#if selectedStop}
		<div class="flex items-center gap-2">
			<span class="w-4 shrink-0 text-[11px] text-muted-foreground tabular-nums">{selectedSortedIndex + 1}</span>

			<NodeNumberInput
				className="min-w-0 flex-1"
				label="Pos"
				value={selectedStop.p}
				min={0}
				max={1}
				step={0.01}
				precision={3}
				onchange={handlePosChange}
			/>

			<!-- Color swatch opens floating picker -->
			<div class="relative shrink-0 overflow-visible">
				<button
					bind:this={swatchEl}
					type="button"
					class="h-6 w-8 shrink-0 cursor-pointer rounded-md border border-border/70 shadow-sm transition hover:border-primary/40"
					style="background-color: {selectedHex};"
					title="Pick stop color"
					onclick={toggleColorPicker}
				></button>

				{#if colorPickerOpen}
					<div
						bind:this={popoverEl}
						class="nodrag nopan absolute top-[calc(100%+0.5rem)] left-0 z-50 rounded-2xl border border-border/80 bg-popover/95 p-3 shadow-[0_24px_80px_rgba(0,0,0,0.45)] backdrop-blur-xl"
						data-gradient-color-picker
					>
						<ColorPickerPanel hex={selectedHex} onchange={handleColorInput} />
					</div>
				{/if}
			</div>
			<span class="font-mono text-[11px] text-muted-foreground">{selectedHex}</span>
		</div>
	{/if}
</div>
