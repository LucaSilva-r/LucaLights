<script lang="ts">
	import { rgb, type RgbColor } from "$lib/lucalights";

	type PreviewStripVariant = 'strip' | 'swatch-grid';

	let {
		colors = [],
		variant = 'strip'
	}: {
		colors?: RgbColor[];
		variant?: PreviewStripVariant;
	} = $props();

</script>

<div class={variant === 'swatch-grid'
	? 'rounded-lg border border-border/70 bg-zinc-950/85 p-1.5 shadow-inner'
	: 'rounded-xl border border-border/70 bg-zinc-950/85 p-2 shadow-inner'}
>
	{#if colors.length === 0}
		<div class={variant === 'swatch-grid'
			? 'flex h-7 items-center justify-center rounded-md border border-dashed border-white/10 text-xs text-zinc-400'
			: 'flex h-10 items-center justify-center rounded-lg border border-dashed border-white/10 text-xs text-zinc-400'}
		>
			No preview frame yet
		</div>
	{:else if variant === 'swatch-grid'}
		<div class="flex h-3 gap-px overflow-hidden rounded-md">
			{#each colors as color}
				<span
					class="min-w-0 flex-1 rounded-[1px]"
					style={`background: ${rgb(color)}`}
					aria-hidden="true"
				></span>
			{/each}
		</div>
	{:else}
		<div class="flex h-10 gap-px overflow-hidden rounded-lg">
			{#each colors as color}
				<span class="min-w-0 flex-1 rounded-[2px]" style={`background: ${rgb(color)}`} aria-hidden="true"></span>
			{/each}
		</div>
	{/if}
</div>
