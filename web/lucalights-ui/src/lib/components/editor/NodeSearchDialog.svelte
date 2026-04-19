<script lang="ts">
	import { onMount } from 'svelte';
	import {
		Dialog,
		DialogContent,
		DialogHeader,
		DialogTitle,
	} from '$lib/components/ui/dialog';
	import { Search, Plus } from '@lucide/svelte';
	import type { NodeTypeDefinition } from '$lib/lucalights';

	interface Props {
		nodeTypes: NodeTypeDefinition[];
		open: boolean;
		onSelect: (typeId: string) => void;
		onClose: () => void;
	}

	let { nodeTypes, open = $bindable(), onSelect, onClose }: Props = $props();

	let searchTerm = $state('');
	let selectedIndex = $state(0);
	let inputElement: HTMLInputElement | undefined = $state();
	let itemRefs = $state<HTMLElement[]>([]);

	let filteredNodes = $derived.by(() => {
		const term = searchTerm.trim().toLowerCase();
		if (!term) return nodeTypes;
		return nodeTypes.filter((node) =>
			node.displayName.toLowerCase().includes(term) ||
			node.typeId.toLowerCase().includes(term) ||
			node.category.toLowerCase().includes(term)
		);
	});

	// Reset selection and refs when search term changes
	$effect(() => {
		searchTerm;
		selectedIndex = 0;
		itemRefs = [];
	});

	// Auto-focus input when dialog opens
	$effect(() => {
		if (open) {
			setTimeout(() => {
				inputElement?.focus();
			}, 50);
		}
	});

	function handleSelect() {
		if (filteredNodes.length > 0) {
			onSelect(filteredNodes[selectedIndex].typeId);
			close();
		}
	}

	function close() {
		onClose();
		searchTerm = '';
		selectedIndex = 0;
		itemRefs = [];
	}

	function handleKeyDown(event: KeyboardEvent) {
		if (!open) return;

		if (event.key === 'ArrowDown') {
			event.preventDefault();
			selectedIndex = (selectedIndex + 1) % filteredNodes.length;
			setTimeout(() => {
				if (itemRefs[selectedIndex]) {
					itemRefs[selectedIndex].scrollIntoView({ block: 'nearest' });
				}
			}, 0);
		} else if (event.key === 'ArrowUp') {
			event.preventDefault();
			selectedIndex = (selectedIndex - 1 + filteredNodes.length) % filteredNodes.length;
			setTimeout(() => {
				if (itemRefs[selectedIndex]) {
					itemRefs[selectedIndex].scrollIntoView({ block: 'nearest' });
				}
			}, 0);
		} else if (event.key === 'Enter') {
			event.preventDefault();
			handleSelect();
		} else if (event.key === 'Escape') {
			event.preventDefault();
			close();
		}
	}
</script>

<svelte:window onkeydown={handleKeyDown} />

<Dialog bind:open onOpenChange={(v) => !v && close()}>
	<DialogContent class="max-w-md p-0 overflow-hidden">
		<DialogHeader class="p-4 pb-0">
			<DialogTitle class="flex items-center gap-2 text-lg">
				<Search class="size-5 text-muted-foreground" />
				Add Node
			</DialogTitle>
		</DialogHeader>

		<div class="p-4">
			<div class="relative">
				<Search
					class="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
				/>
				<input
					bind:this={inputElement}
					bind:value={searchTerm}
					placeholder="Search nodes..."
					class="h-10 w-full rounded-xl border border-border/70 bg-surface-glass pl-9 pr-3 text-sm shadow-sm outline-none transition focus:border-ring focus:ring-4 focus:ring-ring/20"
				/>
			</div>
		</div>

		<div class="max-h-[300px] overflow-y-auto px-2 pb-4">
			{#if filteredNodes.length > 0}
				<div class="space-y-1">
					{#each filteredNodes as node, i (node.typeId)}
						<button
							bind:this={itemRefs[i]}
							type="button"
							class="flex w-full items-center justify-between rounded-lg px-3 py-2 text-left text-sm transition-colors
                            {i === selectedIndex 
                                ? 'bg-primary/10 text-primary ring-1 ring-primary/20' 
                                : 'hover:bg-muted'}"
							onclick={handleSelect}
						>
							<div class="flex flex-col">
								<span class="font-medium">{node.displayName}</span>
								<span class="text-xs text-muted-foreground">{node.category}</span>
							</div>
							<Plus class="size-4 text-muted-foreground/50" />
						</button>
					{/each}
				</div>
			{:else}
				<div class="py-8 text-center text-sm text-muted-foreground">
					No nodes found.
				</div>
			{/if}
		</div>
	</DialogContent>
</Dialog>
