<script lang="ts">
	import { ChevronLeft, ChevronRight } from '@lucide/svelte';

	let {
		value = 0,
		label = '',
		min = undefined,
		max = undefined,
		step = 1,
		precision = 3,
		className = '',
		onchange
	}: {
		value?: number;
		label?: string;
		min?: number;
		max?: number;
		step?: number;
		precision?: number;
		className?: string;
		onchange?: (value: number) => void;
	} = $props();

	let inputEl: HTMLInputElement | undefined = $state();
	let draft = $state('');

	function clampValue(next: number) {
		let clamped = next;
		if (min !== undefined) clamped = Math.max(min, clamped);
		if (max !== undefined) clamped = Math.min(max, clamped);
		return clamped;
	}

	function formatValue(next: number) {
		if (!Number.isFinite(next)) {
			return '0';
		}

		if (precision <= 0) {
			return String(Math.round(next));
		}

		return next.toFixed(precision).replace(/\.?0+$/, '') || '0';
	}

	$effect(() => {
		if (inputEl && document.activeElement === inputEl) {
			return;
		}

		draft = formatValue(value);
	});

	function commitValue(next: number) {
		if (!Number.isFinite(next)) {
			draft = formatValue(value);
			return;
		}

		const clamped = clampValue(next);
		draft = formatValue(clamped);
		onchange?.(clamped);
	}

	function commitDraft() {
		const parsed = Number(draft);
		commitValue(parsed);
	}

	function nudge(direction: -1 | 1) {
		commitValue(value + step * direction);
	}

	function handleKeydown(event: KeyboardEvent) {
		if (event.key === 'Enter') {
			commitDraft();
			inputEl?.blur();
			return;
		}

		if (event.key === 'Escape') {
			draft = formatValue(value);
			inputEl?.blur();
			return;
		}

		if (event.key === 'ArrowUp') {
			event.preventDefault();
			nudge(1);
			return;
		}

		if (event.key === 'ArrowDown') {
			event.preventDefault();
			nudge(-1);
		}
	}
</script>

<div
	class={`nodrag nopan group relative flex h-7 min-w-0 items-center overflow-hidden rounded-md border border-border/70 bg-background/90 shadow-sm transition focus-within:border-ring focus-within:ring-4 focus-within:ring-ring/20 ${className}`}
>
	<button
		type="button"
		tabindex="-1"
		class="absolute inset-y-0 left-0 z-10 flex w-6 items-center justify-center text-muted-foreground/80 opacity-0 transition group-hover:opacity-100 group-focus-within:opacity-100 hover:text-foreground"
		onclick={() => nudge(-1)}
		aria-label={label ? `Decrease ${label}` : 'Decrease value'}
	>
		<ChevronLeft class="size-3.5" />
	</button>

	<div class="flex min-w-0 flex-1 items-center gap-2 px-2 pl-6 pr-6">
		{#if label}
			<span class="shrink-0 text-[12px] font-medium text-muted-foreground">{label}</span>
		{/if}
		<input
			bind:this={inputEl}
			class="h-full min-w-0 flex-1 bg-transparent text-right text-[11px] tabular-nums text-foreground outline-none [appearance:textfield] [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none"
			type="text"
			inputmode="decimal"
			value={draft}
			oninput={(event) => (draft = (event.currentTarget as HTMLInputElement).value)}
			onblur={commitDraft}
			onkeydown={handleKeydown}
		/>
	</div>

	<button
		type="button"
		tabindex="-1"
		class="absolute inset-y-0 right-0 z-10 flex w-6 items-center justify-center text-muted-foreground/80 opacity-0 transition group-hover:opacity-100 group-focus-within:opacity-100 hover:text-foreground"
		onclick={() => nudge(1)}
		aria-label={label ? `Increase ${label}` : 'Increase value'}
	>
		<ChevronRight class="size-3.5" />
	</button>
</div>
