<script lang="ts">
	import { rgb, type RgbColor } from "$lib/lucalights";

	let { colors = [] }: { colors?: RgbColor[] } = $props();
</script>

<div class="rounded-xl border border-border/70 bg-zinc-950/85 p-2 shadow-inner">
	{#if colors.length === 0}
		<div class="flex h-10 items-center justify-center rounded-lg border border-dashed border-white/10 text-xs text-zinc-400">
			No preview frame yet
		</div>
	{:else}
		<div class="flex h-10 gap-px overflow-hidden rounded-lg">
			{#each colors as color}
				<span class="min-w-0 flex-1 rounded-[2px]" style={`background: ${rgb(color)}`} aria-hidden="true"></span>
			{/each}
		</div>
	{/if}
</div>
