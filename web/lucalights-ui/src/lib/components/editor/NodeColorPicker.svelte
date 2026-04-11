<script lang="ts">
	import ColorPickerPanel from './ColorPickerPanel.svelte';

	let {
		hex = '#ffffff',
		label = 'Color',
		onchange
	}: {
		hex?: string;
		label?: string;
		onchange?: (hex: string) => void;
	} = $props();

	let isOpen = $state(false);
	let swatchEl: HTMLButtonElement | undefined = $state();
	let popoverEl: HTMLDivElement | undefined = $state();

	// Close on click outside
	$effect(() => {
		if (!isOpen) return;
		function handleClickOutside(event: PointerEvent) {
			const target = event.target as HTMLElement;
			if (swatchEl?.contains(target)) return;
			if (popoverEl?.contains(target)) return;
			isOpen = false;
		}
		document.addEventListener('pointerdown', handleClickOutside, true);
		return () => document.removeEventListener('pointerdown', handleClickOutside, true);
	});

	function handleInput(nextHex: string) {
		onchange?.(nextHex);
	}

	function togglePicker() {
		isOpen = !isOpen;
	}
</script>

<div class="nodrag nopan relative flex h-7 items-center gap-2 overflow-visible">
	<button
		bind:this={swatchEl}
		type="button"
		class="h-6 w-8 shrink-0 cursor-pointer rounded-md border border-border/70 shadow-sm transition hover:border-primary/40"
		style="background-color: {hex};"
		title="Pick color"
		onclick={togglePicker}
	></button>
	<span class="text-[12px] font-medium">{label}</span>
	<span class="ml-auto font-mono text-[11px] text-muted-foreground">{hex}</span>

	{#if isOpen}
		<div
			bind:this={popoverEl}
			class="nodrag nopan absolute top-[calc(100%+0.5rem)] left-0 z-50 rounded-2xl border border-border/80 bg-popover/95 p-3 shadow-[0_24px_80px_rgba(0,0,0,0.45)] backdrop-blur-xl"
			data-node-color-picker
		>
			<ColorPickerPanel {hex} onchange={handleInput} />
		</div>
	{/if}
</div>
