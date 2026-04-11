<script lang="ts">
	import { Check, ChevronDown, Search, X } from '@lucide/svelte';
	import * as Dialog from '$lib/components/ui/dialog';
	import type { EditorDeviceOption, EditorSegmentOption } from './types';

	type GroupedDevice = {
		id: string;
		label: string;
		segments: EditorSegmentOption[];
	};

	let {
		value = '',
		devices = [],
		segments = [],
		onChange
	}: {
		value?: string;
		devices?: EditorDeviceOption[];
		segments?: EditorSegmentOption[];
		onChange?: (value: string) => void;
	} = $props();

	let open = $state(false);
	let search = $state('');
	let openDeviceIds = $state<string[]>([]);

	let normalizedSegments = $derived.by(() =>
		segments.filter(
			(segment) =>
				typeof segment.id === 'string' &&
				segment.id.trim().length > 0 &&
				typeof segment.name === 'string' &&
				segment.name.trim().length > 0
		)
	);

	let allDevices = $derived.by(() => {
		const deviceMap = new Map<string, GroupedDevice>();

		for (const device of devices) {
			if (typeof device.id !== 'string' || device.id.trim().length === 0) {
				continue;
			}

			deviceMap.set(device.id, {
				id: device.id,
				label: device.name?.trim() || device.id,
				segments: []
			});
		}

		for (const segment of normalizedSegments) {
			let device = deviceMap.get(segment.deviceId);
			if (!device) {
				device = {
					id: segment.deviceId,
					label: segment.deviceName?.trim() || segment.deviceId,
					segments: []
				};
				deviceMap.set(segment.deviceId, device);
			}

			device.segments.push(segment);
		}

		return Array.from(deviceMap.values()).filter((device) => device.segments.length > 0);
	});

	let segmentMap = $derived.by(
		() => new Map(normalizedSegments.map((segment) => [segment.id.toLowerCase(), segment]))
	);

	let selectedIds = $derived.by(() => parseIds(value));
	let selectedIdSet = $derived.by(() => new Set(selectedIds.map((id) => id.toLowerCase())));

	let selectedSegments = $derived.by(() =>
		selectedIds.map((id) => segmentMap.get(id.toLowerCase()) ?? fallbackSegment(id))
	);

	let visibleDevices = $derived.by(() => {
		const query = search.trim().toLowerCase();

		return allDevices
			.map((device) => {
				if (query.length === 0) {
					return device;
				}

				if (device.label.toLowerCase().includes(query)) {
					return device;
				}

				const matchingSegments = device.segments.filter(
					(segment) =>
						segment.name.toLowerCase().includes(query) ||
						segment.id.toLowerCase().includes(query)
				);

				return matchingSegments.length > 0
					? {
							...device,
							segments: matchingSegments
						}
					: null;
			})
			.filter((device): device is GroupedDevice => device !== null);
	});

	$effect(() => {
		if (!open) {
			search = '';
			return;
		}

		if (openDeviceIds.length === 0 && visibleDevices.length > 0) {
			openDeviceIds = defaultOpenDeviceIds();
		}
	});

	function parseIds(input: string) {
		return input
			.split(',')
			.map((part) => part.trim())
			.filter((part, index, values) => part.length > 0 && values.indexOf(part) === index);
	}

	function fallbackSegment(id: string): EditorSegmentOption {
		return {
			id,
			name: id,
			deviceId: '',
			deviceName: 'Selected'
		};
	}

	function selectionLabel() {
		if (selectedSegments.length === 0) {
			return 'All segments';
		}

		if (selectedSegments.length === 1) {
			return selectedSegments[0]?.name ?? '1 segment selected';
		}

		return `${selectedSegments.length} segments selected`;
	}

	function selectionTooltip() {
		if (selectedSegments.length === 0) {
			return 'All output segments';
		}

		return selectedSegments
			.map((segment) => `${segment.deviceName} · ${segment.name}`)
			.join('\n');
	}

	function clearSelection() {
		onChange?.('');
	}

	function isSelected(segmentId: string) {
		return selectedIdSet.has(segmentId.toLowerCase());
	}

	function toggleSegment(segmentId: string) {
		const nextIds = [...selectedIds];
		const matchIndex = nextIds.findIndex((candidate) => candidate.toLowerCase() === segmentId.toLowerCase());

		if (matchIndex >= 0) {
			nextIds.splice(matchIndex, 1);
		} else {
			nextIds.push(segmentId);
		}

		onChange?.(nextIds.join(', '));
	}

	function deviceSelectionCount(deviceId: string) {
		return allDevices
			.find((device) => device.id === deviceId)
			?.segments.filter((segment) => isSelected(segment.id)).length ?? 0;
	}

	function isDeviceFullySelected(deviceId: string) {
		const device = allDevices.find((entry) => entry.id === deviceId);
		return !!device && device.segments.length > 0 && device.segments.every((segment) => isSelected(segment.id));
	}

	function toggleDeviceSelection(deviceId: string) {
		const device = allDevices.find((entry) => entry.id === deviceId);
		if (!device) {
			return;
		}

		const nextIds = [...selectedIds];

		if (isDeviceFullySelected(deviceId)) {
			for (const segment of device.segments) {
				const index = nextIds.findIndex((candidate) => candidate.toLowerCase() === segment.id.toLowerCase());
				if (index >= 0) {
					nextIds.splice(index, 1);
				}
			}
		} else {
			for (const segment of device.segments) {
				if (!nextIds.some((candidate) => candidate.toLowerCase() === segment.id.toLowerCase())) {
					nextIds.push(segment.id);
				}
			}
		}

		onChange?.(nextIds.join(', '));
	}

	function defaultOpenDeviceIds() {
		const selectedDeviceIds = Array.from(
			new Set(selectedSegments.map((segment) => segment.deviceId).filter((deviceId) => deviceId.length > 0))
		);

		if (selectedDeviceIds.length > 0) {
			return selectedDeviceIds;
		}

		return visibleDevices.length > 0 ? [visibleDevices[0].id] : [];
	}

	function deviceContainsSelection(deviceId: string) {
		return selectedSegments.some((segment) => segment.deviceId === deviceId);
	}

	function isDeviceOpen(deviceId: string) {
		return search.trim().length > 0 || openDeviceIds.includes(deviceId);
	}

	function toggleDevicePanel(deviceId: string) {
		if (search.trim().length > 0) {
			return;
		}

		openDeviceIds = openDeviceIds.includes(deviceId)
			? openDeviceIds.filter((value) => value !== deviceId)
			: [...openDeviceIds, deviceId];
	}
</script>

<Dialog.Root bind:open>
	<div class="w-full">
		<button
			type="button"
			class="nodrag nopan flex h-7 w-full items-center gap-2 rounded-md border border-border/70 bg-background/90 px-2 text-left text-[11px] shadow-sm outline-none transition hover:border-primary/40 focus:border-ring focus:ring-4 focus:ring-ring/20"
			title={selectionTooltip()}
			onclick={() => {
				open = true;
			}}
		>
			<span class="min-w-0 flex-1 truncate">{selectionLabel()}</span>
			{#if selectedSegments.length > 0}
				<span class="rounded-full border border-border/70 bg-secondary px-1.5 py-0.5 text-[10px] text-secondary-foreground">
					{selectedSegments.length}
				</span>
			{/if}
			<ChevronDown class="size-3.5 text-muted-foreground" />
		</button>
	</div>

	<Dialog.Content class="max-h-[85vh] w-[min(56rem,calc(100vw-1rem))] overflow-hidden border-surface-card-border bg-background p-0 sm:max-w-[56rem]">
		<div class="flex max-h-[85vh] flex-col overflow-hidden">
			<div class="border-b border-border/60 px-5 py-5 pr-12">
				<Dialog.Header class="gap-2">
					<Dialog.Title class="text-base">Output Targets</Dialog.Title>
					<Dialog.Description>
						Pick which device segments this output writes to. Leave empty to target every segment.
					</Dialog.Description>
				</Dialog.Header>

				<div class="mt-4 flex items-center gap-3">
					<label class="relative min-w-0 flex-1">
						<Search class="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
						<input
							class="nodrag nopan h-10 w-full rounded-xl border border-border/70 bg-background/80 pl-10 pr-3 text-sm shadow-sm outline-none transition focus:border-ring focus:ring-4 focus:ring-ring/20"
							type="text"
							placeholder="Search devices or segments"
							bind:value={search}
						/>
					</label>

					{#if selectedSegments.length > 0}
						<button
							type="button"
							class="rounded-lg border border-border/70 px-3 py-2 text-xs text-muted-foreground transition hover:border-primary/30 hover:text-foreground"
							onclick={clearSelection}
						>
							All segments
						</button>
					{/if}
				</div>
			</div>

			{#if selectedSegments.length > 0}
				<div class="border-b border-border/60 px-5 py-4">
					<div class="flex items-center justify-between gap-3">
						<p class="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
							Selected
						</p>
						<p class="text-[11px] text-muted-foreground">{selectedSegments.length} chosen</p>
					</div>

					<div class="mt-3 flex flex-wrap gap-2">
						{#each selectedSegments as segment}
							<button
								type="button"
								class="flex max-w-full items-center gap-1 rounded-full border border-primary/20 bg-primary/10 px-3 py-1.5 text-xs text-primary transition hover:border-primary/40 hover:bg-primary/15"
								title={`${segment.deviceName} · ${segment.name}`}
								onclick={() => toggleSegment(segment.id)}
							>
								<span class="truncate">{segment.name}</span>
								<X class="size-3 shrink-0" />
							</button>
						{/each}
					</div>
				</div>
			{/if}

			<div class="min-h-0 flex-1 overflow-y-auto overflow-x-hidden px-5 py-5">
				{#if visibleDevices.length === 0}
					<p class="rounded-xl border border-dashed border-border/70 px-4 py-10 text-center text-sm text-muted-foreground">
						No segments match the current search.
					</p>
				{:else}
					<div class="space-y-6">
						{#each visibleDevices as device}
							<section class="rounded-2xl border border-border/60 bg-background">
								<div class="flex items-center gap-3 px-4 py-3">
									<button
										type="button"
										class="min-w-0 flex-1 text-left transition hover:text-foreground"
										onclick={() => toggleDevicePanel(device.id)}
									>
										<p class="truncate text-sm font-semibold text-foreground">{device.label}</p>
										<p class="text-[11px] text-muted-foreground">
											{deviceSelectionCount(device.id)} of {allDevices.find((entry) => entry.id === device.id)?.segments.length ?? device.segments.length} segments selected
										</p>
									</button>

									<button
										type="button"
										class={`rounded-full border px-2.5 py-1 text-[10px] font-medium transition ${
											isDeviceFullySelected(device.id)
												? 'border-primary/30 bg-primary/10 text-primary'
												: 'border-border/70 bg-background/80 text-muted-foreground hover:border-primary/20 hover:text-foreground'
										}`}
										onclick={() => toggleDeviceSelection(device.id)}
									>
										{isDeviceFullySelected(device.id) ? 'Clear' : 'All'}
									</button>

									<div class="flex items-center gap-2">
										{#if deviceContainsSelection(device.id)}
											<span class="rounded-full border border-primary/20 bg-primary/10 px-1.5 py-0.5 text-[10px] text-primary">
												selected
											</span>
										{/if}
										<ChevronDown
											class={`size-4 text-muted-foreground transition ${
												isDeviceOpen(device.id) ? 'rotate-180' : ''
											}`}
										/>
									</div>
								</div>

								{#if isDeviceOpen(device.id)}
									<div class="grid gap-2 border-t border-border/60 px-4 py-4 sm:grid-cols-2">
										{#each device.segments as segment}
											<button
												type="button"
												class={`flex min-w-0 items-center justify-between gap-3 rounded-xl border px-3 py-3 text-left transition ${
													isSelected(segment.id)
														? 'border-primary/40 bg-primary/10'
														: 'border-border/70 bg-card hover:border-primary/20 hover:bg-muted'
												}`}
												title={`${segment.deviceName} · ${segment.id}`}
												onclick={() => toggleSegment(segment.id)}
											>
												<p class="min-w-0 flex-1 truncate text-sm font-medium text-foreground">
													{segment.name}
												</p>
												<div class="flex size-5 shrink-0 items-center justify-center rounded-full border border-border/70 bg-background/80">
													{#if isSelected(segment.id)}
														<Check class="size-3 text-primary" />
													{/if}
												</div>
											</button>
										{/each}
									</div>
								{/if}
							</section>
						{/each}
					</div>
				{/if}
			</div>
		</div>
	</Dialog.Content>
</Dialog.Root>
