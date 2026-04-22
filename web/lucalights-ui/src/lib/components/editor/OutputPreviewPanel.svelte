<script lang="ts">
	import { Activity, ChevronRight, MonitorUp } from '@lucide/svelte';
	import { onMount } from 'svelte';
	import PreviewStrip from '$lib/components/PreviewStrip.svelte';
	import { Badge } from '$lib/components/ui/badge';
	import { Button } from '$lib/components/ui/button';
	import {
		createSocket,
		previewPayloadFromTopology,
		toMessage,
		type Device,
		type PreviewDevice,
		type PreviewPayload,
		type PreviewSegment,
		type PreviewTopology,
		type RuntimeEnvelope
	} from '$lib/lucalights';

	type SelectedOutputTarget = {
		label: string;
		segmentIds: string[];
		targetsAll: boolean;
	};

	type SegmentOption = {
		id: string;
		name: string;
		deviceId: string;
		deviceName: string;
		groupIds: number[];
	};

	type GroupOption = {
		groupId: number;
		label: string;
		detail: string;
		segmentIds: string[];
	};

	type TargetOption = {
		value: string;
		label: string;
	};

	type ResolvedTarget =
		| { kind: 'all'; label: string }
		| { kind: 'selected'; label: string; segmentIds: Set<string>; targetsAll: boolean }
		| { kind: 'device'; label: string; deviceId: string }
		| { kind: 'group'; label: string; segmentIds: Set<string> }
		| { kind: 'segment'; label: string; segmentId: string };

	let {
		devices = [],
		selectedOutputTarget = null,
		onCollapse
	}: {
		devices?: Device[];
		selectedOutputTarget?: SelectedOutputTarget | null;
		onCollapse?: () => void;
	} = $props();

	let previewPayload = $state<PreviewPayload | null>(null);
	let previewTopology = $state<PreviewTopology | null>(null);
	let previewConnected = $state(false);
	let previewTarget = $state('all');
	let errorMessage = $state('');

	let segmentOptions = $derived.by(() =>
		devices.flatMap((device) =>
			device.segments.map(
				(segment): SegmentOption => ({
					id: segment.id,
					name: segment.name,
					deviceId: device.id,
					deviceName: device.name,
					groupIds: segment.groupIds ?? []
				})
			)
		)
	);

	let groupOptions = $derived.by(() => {
		const groups = new Map<number, { segmentIds: Set<string>; deviceIds: Set<string> }>();

		for (const segment of segmentOptions) {
			for (const groupId of segment.groupIds) {
				if (!Number.isFinite(groupId)) {
					continue;
				}

				let group = groups.get(groupId);
				if (!group) {
					group = {
						segmentIds: new Set<string>(),
						deviceIds: new Set<string>()
					};
					groups.set(groupId, group);
				}

				group.segmentIds.add(segment.id);
				group.deviceIds.add(segment.deviceId);
			}
		}

		return Array.from(groups.entries())
			.sort(([left], [right]) => left - right)
			.map(
				([groupId, group]): GroupOption => ({
					groupId,
					label: `Group ${groupId}`,
					detail: `${group.segmentIds.size} segment${group.segmentIds.size === 1 ? '' : 's'} on ${group.deviceIds.size} device${group.deviceIds.size === 1 ? '' : 's'}`,
					segmentIds: Array.from(group.segmentIds)
				})
			);
	});

	let targetOptions = $derived.by(() => {
		const options: TargetOption[] = [{ value: 'all', label: 'All devices' }];

		if (selectedOutputTarget) {
			options.unshift({
				value: 'selected',
				label: `Selected output: ${selectedOutputTarget.label}`
			});
		}

		for (const device of devices) {
			options.push({
				value: `device:${device.id}`,
				label: `Device: ${device.name}`
			});
		}

		for (const group of groupOptions) {
			options.push({
				value: `group:${group.groupId}`,
				label: `${group.label}: ${group.detail}`
			});
		}

		for (const segment of segmentOptions) {
			options.push({
				value: `segment:${segment.id}`,
				label: `Segment: ${segment.deviceName} / ${segment.name}`
			});
		}

		return options;
	});

	let targetOptionValues = $derived(new Set(targetOptions.map((option) => option.value)));
	let effectiveTarget = $derived.by(resolveTarget);
	let previewDevices = $derived(previewPayload?.devices ?? []);
	let filteredPreviewDevices = $derived.by(() => filterPreviewDevices(previewDevices, effectiveTarget));
	$effect(() => {
		if (!targetOptionValues.has(previewTarget)) {
			previewTarget = 'all';
		}
	});

	function resolveTarget(): ResolvedTarget {
		if (previewTarget === 'selected' && selectedOutputTarget) {
			return {
				kind: 'selected',
				label: selectedOutputTarget.targetsAll
					? `${selectedOutputTarget.label} / all segments`
					: selectedOutputTarget.label,
				segmentIds: normalizeIds(selectedOutputTarget.segmentIds),
				targetsAll: selectedOutputTarget.targetsAll
			};
		}

		if (previewTarget.startsWith('device:')) {
			const deviceId = previewTarget.slice('device:'.length);
			const device = devices.find((candidate) => candidate.id === deviceId);

			if (device) {
				return {
					kind: 'device',
					label: device.name,
					deviceId
				};
			}
		}

		if (previewTarget.startsWith('group:')) {
			const groupId = Number(previewTarget.slice('group:'.length));
			const group = groupOptions.find((candidate) => candidate.groupId === groupId);

			if (group) {
				return {
					kind: 'group',
					label: group.label,
					segmentIds: normalizeIds(group.segmentIds)
				};
			}
		}

		if (previewTarget.startsWith('segment:')) {
			const segmentId = previewTarget.slice('segment:'.length);
			const segment = segmentOptions.find((candidate) => candidate.id === segmentId);

			if (segment) {
				return {
					kind: 'segment',
					label: `${segment.deviceName} / ${segment.name}`,
					segmentId: segmentId.toLowerCase()
				};
			}
		}

		return {
			kind: 'all',
			label: 'All devices'
		};
	}

	function normalizeIds(ids: string[]) {
		return new Set(ids.map((id) => id.trim().toLowerCase()).filter((id) => id.length > 0));
	}

	function filterPreviewDevices(devicesToFilter: PreviewDevice[], target: ResolvedTarget) {
		return devicesToFilter
			.map((device) => {
				if (target.kind === 'device' && device.id !== target.deviceId) {
					return null;
				}

				const segments = device.segments.filter((segment) => segmentMatchesTarget(device, segment, target));
				if (segments.length === 0) {
					return null;
				}

				return {
					...device,
					ledCount: segments.reduce((total, segment) => total + segment.length, 0),
					segments
				};
			})
			.filter((device): device is PreviewDevice => device !== null);
	}

	function segmentMatchesTarget(device: PreviewDevice, segment: PreviewSegment, target: ResolvedTarget) {
		switch (target.kind) {
			case 'all':
			case 'device':
				return true;
			case 'selected':
				return target.targetsAll || target.segmentIds.has(segment.id.toLowerCase());
			case 'group':
				return target.segmentIds.has(segment.id.toLowerCase());
			case 'segment':
				return segment.id.toLowerCase() === target.segmentId;
		}
	}

	function handlePreviewEvent(envelope: RuntimeEnvelope) {
		switch (envelope.type) {
			case 'preview.connected':
				previewConnected = true;
				return;
			case 'preview.topology':
				previewTopology = envelope.payload as PreviewTopology;
				previewPayload = previewPayloadFromTopology(previewTopology, new Uint8Array());
				return;
		}
	}

	function handlePreviewFrame(frame: ArrayBuffer) {
		if (!previewTopology) {
			return;
		}

		previewPayload = previewPayloadFromTopology(previewTopology, frame);
	}

	onMount(() => {
		let disposed = false;
		let socket: WebSocket | null = null;
		let reconnectTimer: number | undefined;

		const openSocket = () => {
			if (disposed) {
				return;
			}

			try {
				socket = createSocket('/ws/preview');
			} catch (error) {
				errorMessage = toMessage(error);
				return;
			}

			socket.addEventListener('open', () => {
				previewConnected = true;
				errorMessage = '';
			});

			socket.addEventListener('message', (event) => {
				try {
					if (event.data instanceof ArrayBuffer) {
						handlePreviewFrame(event.data);
					} else {
						handlePreviewEvent(JSON.parse(event.data) as RuntimeEnvelope);
					}
					errorMessage = '';
				} catch (error) {
					errorMessage = toMessage(error);
				}
			});

			socket.addEventListener('close', () => {
				previewConnected = false;

				if (!disposed) {
					reconnectTimer = window.setTimeout(openSocket, 2_000);
				}
			});

			socket.addEventListener('error', () => {
				socket?.close();
			});
		};

		openSocket();

		return () => {
			disposed = true;

			if (reconnectTimer) {
				window.clearTimeout(reconnectTimer);
			}

			socket?.close();
		};
	});
</script>

<aside class="min-h-0 border-l border-border/60 bg-(image:--editor-sidebar)">
	<div class="flex h-full min-h-0 flex-col">
		<div class="border-b border-border/60 p-4">
			<div class="flex items-start justify-between gap-3">
				<div class="min-w-0 space-y-1">
					<div class="flex items-center gap-2">
						<MonitorUp class="size-4 text-muted-foreground" />
						<h2 class="truncate text-sm font-semibold tracking-tight">Live Preview</h2>
					</div>
					<p class="truncate text-xs text-muted-foreground">{effectiveTarget.label}</p>
				</div>
				<div class="flex shrink-0 items-center gap-2">
					<Badge variant={previewConnected ? 'default' : 'secondary'}>
						<Activity />
						{previewConnected ? 'Live' : 'Retrying'}
					</Badge>
					<Button
						variant="ghost"
						size="icon-sm"
						aria-label="Hide live preview"
						title="Hide live preview"
						onclick={() => onCollapse?.()}
					>
						<ChevronRight />
					</Button>
				</div>
			</div>
		</div>

		<div class="min-h-0 flex-1 overflow-auto p-4">
			<div class="space-y-4">
				<label class="block space-y-1.5">
					<span class="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
						Target
					</span>
					<select
						bind:value={previewTarget}
						class="h-9 w-full rounded-lg border border-border/70 bg-background/90 px-3 text-sm shadow-sm outline-none transition focus:border-ring focus:ring-4 focus:ring-ring/20"
					>
						{#each targetOptions as option (option.value)}
							<option value={option.value}>{option.label}</option>
						{/each}
					</select>
				</label>

				{#if errorMessage}
					<div class="rounded-lg border border-destructive/30 bg-destructive/10 px-3 py-2 text-xs text-destructive">
						{errorMessage}
					</div>
				{/if}

				{#if filteredPreviewDevices.length > 0}
					<div class="space-y-3">
						{#each filteredPreviewDevices as device (device.id)}
							<section class="rounded-xl border border-border/70 bg-background/55 p-3 shadow-sm">
								<div class="flex items-start justify-between gap-3">
									<div class="min-w-0">
										<p class="truncate text-sm font-semibold">{device.name}</p>
									</div>
									<Badge variant="outline">{device.ledCount}</Badge>
								</div>

								<div class="mt-2 space-y-2">
									{#each device.segments as segment (segment.id)}
										<div class="space-y-1.5">
											<div class="flex items-center justify-between gap-3 text-xs">
												<p class="min-w-0 truncate font-medium">{segment.name}</p>
												<p class="shrink-0 font-mono text-muted-foreground">
													{segment.colors.length}/{segment.length}
												</p>
											</div>
											<PreviewStrip colors={segment.colors} variant="swatch-grid" />
										</div>
									{/each}
								</div>
							</section>
						{/each}
					</div>
				{:else}
					<div class="rounded-xl border border-dashed border-border/80 bg-background/45 px-4 py-8 text-center text-sm text-muted-foreground">
						Waiting for preview frames.
					</div>
				{/if}
			</div>
		</div>
	</div>
</aside>
