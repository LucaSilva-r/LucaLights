<script lang="ts">
	import { X } from "@lucide/svelte";
	import { Dialog as DialogPrimitive } from "bits-ui";
	import type { Snippet } from "svelte";
	import { cn, type WithoutChildrenOrChild } from "$lib/utils.js";

	let {
		ref = $bindable(null),
		class: className,
		children,
		showCloseButton = true,
		...restProps
	}: WithoutChildrenOrChild<DialogPrimitive.ContentProps> & {
		children?: Snippet;
		showCloseButton?: boolean;
	} = $props();
</script>

<DialogPrimitive.Portal>
	<DialogPrimitive.Overlay
		data-slot="dialog-overlay"
		class="data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:animate-in data-[state=open]:fade-in-0 fixed inset-0 z-50 bg-black/75 backdrop-blur-sm"
	/>
	<DialogPrimitive.Content
		bind:ref
		data-slot="dialog-content"
		class={cn(
			"bg-background data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:zoom-out-95 data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-95 fixed left-1/2 top-1/2 z-50 w-full max-w-[calc(100vw-1rem)] -translate-x-1/2 -translate-y-1/2 rounded-2xl border border-surface-card-border shadow-2xl duration-200 sm:max-w-3xl",
			className
		)}
		{...restProps}
	>
		{@render children?.()}
		{#if showCloseButton}
			<DialogPrimitive.Close
				class="ring-offset-background focus:ring-ring data-[state=open]:bg-muted absolute right-4 top-4 rounded-md p-1 text-muted-foreground opacity-80 transition hover:opacity-100 focus:outline-none focus:ring-2 focus:ring-offset-2"
			>
				<X class="size-4" />
				<span class="sr-only">Close</span>
			</DialogPrimitive.Close>
		{/if}
	</DialogPrimitive.Content>
</DialogPrimitive.Portal>
