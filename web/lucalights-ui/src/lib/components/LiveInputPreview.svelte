<script lang="ts">
	import { ChevronDown } from '@lucide/svelte';
	import { onDestroy } from 'svelte';
	import Masonry from 'svelte-bricks';
	import { Badge } from '$lib/components/ui/badge';
	import {
		Card,
		CardContent,
		CardDescription,
		CardHeader,
		CardTitle
	} from '$lib/components/ui/card';
	import {
		entriesOf,
		rgb,
		type ColorValue,
		type InputChannelDefinition,
		type InputDefinition,
		type InputSnapshot,
		type InputValueType
	} from '$lib/lucalights';

	type BoolPreviewChannel = InputChannelDefinition & {
		currentValue: boolean;
		pulseLike: boolean;
		pulseActive: boolean;
		pulseToken: number;
		sustainedActive: boolean;
		visualActive: boolean;
	};

	type FloatPreviewChannel = InputChannelDefinition & {
		currentValue: number;
		formattedValue: string;
		percent: number | null;
	};

	type ColorPreviewChannel = InputChannelDefinition & {
		currentValue: ColorValue | null;
	};

	type PreviewGroup = {
		key: string;
		label: string;
		boolChannels: BoolPreviewChannel[];
		floatChannels: FloatPreviewChannel[];
		colorChannels: ColorPreviewChannel[];
		activeBoolCount: number;
		totalCount: number;
	};

	let {
		snapshot = null,
		moduleDefinition = undefined,
		moduleLabel = 'No active module',
		connected = false
	}: {
		snapshot?: InputSnapshot | null;
		moduleDefinition?: InputDefinition;
		moduleLabel?: string;
		connected?: boolean;
	} = $props();

	let collapsedGroupKeys = $state<string[]>([]);
	let pulseStates = $state<Record<string, { token: number }>>({});

	let previousBoolValues: Record<string, boolean> | null = null;
	const pulseTimers = new Map<string, number>();
	const pulseMeta = new Map<string, { token: number; lastTriggeredAt: number }>();

	let metadataEntries = $derived(entriesOf(snapshot?.metadata));

	let boolChannels = $derived.by(() =>
		buildChannels('Bool', snapshot?.boolValues ?? {}, 'Unmapped booleans')
	);
	let floatChannels = $derived.by(() =>
		buildChannels('Float', snapshot?.floatValues ?? {}, 'Unmapped numbers')
	);
	let colorChannels = $derived.by(() =>
		buildChannels('Color', snapshot?.colorValues ?? {}, 'Unmapped colors')
	);
	let pulseLikeBoolKeys = $derived.by(
		() => new Set(boolChannels.filter(isPulseLikeChannel).map((channel) => normalizeKey(channel.key)))
	);

	let previewGroups = $derived.by(() => {
		const groups = new Map<string, PreviewGroup>();

		for (const channel of boolChannels) {
			const currentValue = Boolean(snapshot?.boolValues?.[channel.key]);
			const pulseLike = isPulseLikeChannel(channel);
			const pulseState = pulseStates[normalizeKey(channel.key)];
			const pulseActive = Boolean(pulseState);
			const sustainedActive = currentValue && !pulseLike;
			const previewChannel: BoolPreviewChannel = {
				...channel,
				currentValue,
				pulseLike,
				pulseActive,
				pulseToken: pulseState?.token ?? 0,
				sustainedActive,
				visualActive: sustainedActive || pulseActive
			};
			const group = ensurePreviewGroup(groups, channel);
			group.boolChannels.push(previewChannel);
			group.activeBoolCount += previewChannel.visualActive ? 1 : 0;
			group.totalCount += 1;
		}

		for (const channel of floatChannels) {
			const currentValue = Number(snapshot?.floatValues?.[channel.key] ?? channel.defaultFloatValue ?? 0);
			const previewChannel: FloatPreviewChannel = {
				...channel,
				currentValue,
				formattedValue: formatFloatValue(channel, currentValue),
				percent: floatPercent(channel, currentValue)
			};
			const group = ensurePreviewGroup(groups, channel);
			group.floatChannels.push(previewChannel);
			group.totalCount += 1;
		}

		for (const channel of colorChannels) {
			const previewChannel: ColorPreviewChannel = {
				...channel,
				currentValue: snapshot?.colorValues?.[channel.key] ?? null
			};
			const group = ensurePreviewGroup(groups, channel);
			group.colorChannels.push(previewChannel);
			group.totalCount += 1;
		}

		return Array.from(groups.values()).filter((group) => group.totalCount > 0);
	});

	let activeBoolCount = $derived(
		previewGroups.reduce((total, group) => total + group.activeBoolCount, 0)
	);
	let totalBoolCount = $derived(
		previewGroups.reduce((total, group) => total + group.boolChannels.length, 0)
	);
	let totalFloatCount = $derived(
		previewGroups.reduce((total, group) => total + group.floatChannels.length, 0)
	);
	let totalColorCount = $derived(
		previewGroups.reduce((total, group) => total + group.colorChannels.length, 0)
	);

	$effect(() => {
		if (!connected || !snapshot) {
			previousBoolValues = null;
			clearAllPulses();
			return;
		}

		trackBoolPulses(snapshot, pulseLikeBoolKeys);
	});

	onDestroy(() => {
		clearAllPulses();
	});

	function buildChannels(
		valueType: InputValueType,
		runtimeValues: Record<string, unknown>,
		fallbackGroup: string
	) {
		const channels: InputChannelDefinition[] = [];
		const knownKeys = new Set<string>();

		for (const channel of moduleDefinition?.channels ?? []) {
			if (channelIsType(channel, valueType)) {
				channels.push(channel);
				knownKeys.add(normalizeKey(channel.key));
			}
		}

		for (const key of Object.keys(runtimeValues).sort((left, right) => left.localeCompare(right))) {
			if (!knownKeys.has(normalizeKey(key))) {
				channels.push(fallbackChannel(key, valueType, fallbackGroup));
			}
		}

		return channels;
	}

	function fallbackChannel(
		key: string,
		valueType: InputValueType,
		group: string
	): InputChannelDefinition {
		return {
			key,
			label: labelFromKey(key),
			group,
			valueType,
			category: 'Runtime',
			description: key
		};
	}

	function channelIsType(channel: InputChannelDefinition, valueType: InputValueType) {
		return valueTypeId(channel.valueType) === valueTypeId(valueType);
	}

	function valueTypeId(valueType: InputValueType) {
		if (typeof valueType === 'number') {
			return valueType;
		}

		switch (valueType) {
			case 'Bool':
				return 0;
			case 'Float':
				return 1;
			case 'Color':
				return 2;
			case 'String':
				return 3;
			default:
				return -1;
		}
	}

	function ensurePreviewGroup(groups: Map<string, PreviewGroup>, channel: InputChannelDefinition) {
		const label = groupLabelFor(channel);
		const key = `${moduleDefinition?.moduleId ?? 'active'}::${label.toLowerCase()}`;
		let group = groups.get(key);

		if (!group) {
			group = {
				key,
				label,
				boolChannels: [],
				floatChannels: [],
				colorChannels: [],
				activeBoolCount: 0,
				totalCount: 0
			};
			groups.set(key, group);
		}

		return group;
	}

	function groupLabelFor(channel: InputChannelDefinition) {
		return channel.group?.trim() || channel.category?.trim() || 'Other';
	}

	function normalizeKey(key: string) {
		return key.toLowerCase();
	}

	function labelFromKey(key: string) {
		const tail = key.split('.').pop() ?? key;
		return tail
			.split(/[_\s-]+/)
			.filter((part) => part.length > 0)
			.map((part) => part.charAt(0).toUpperCase() + part.slice(1))
			.join(' ');
	}

	function isPulseLikeChannel(channel: InputChannelDefinition) {
		const searchable = `${channel.key} ${channel.label} ${channel.group} ${channel.description}`.toLowerCase();
		const group = channel.group.toLowerCase();
		const key = channel.key.toLowerCase();
		const label = channel.label.toLowerCase();

		return (
			searchable.includes('pulse') ||
			searchable.includes('one-frame') ||
			searchable.includes('onset') ||
			key.includes('_hit') ||
			label.endsWith(' hit') ||
			key.includes('.button.') ||
			group.includes('button') ||
			group.includes('keys') ||
			group.includes('pad') ||
			group.includes('player')
		);
	}

	function trackBoolPulses(nextSnapshot: InputSnapshot, pulseKeys: Set<string>) {
		if (!previousBoolValues) {
			for (const [key, value] of Object.entries(nextSnapshot.boolValues ?? {})) {
				if (value && pulseKeys.has(normalizeKey(key))) {
					triggerBoolPulse(key);
				}
			}

			previousBoolValues = { ...nextSnapshot.boolValues };
			return;
		}

		for (const [key, value] of Object.entries(nextSnapshot.boolValues ?? {})) {
			if (!value || !pulseKeys.has(normalizeKey(key))) {
				continue;
			}

			const wasActive = previousBoolValues[key];
			const shouldRetrigger = !wasActive || shouldRetriggerHeldPulse(key);

			if (shouldRetrigger) {
				triggerBoolPulse(key);
			}
		}

		previousBoolValues = { ...nextSnapshot.boolValues };
	}

	function triggerBoolPulse(key: string) {
		const normalizedKey = normalizeKey(key);
		const now = Date.now();
		const meta = pulseMeta.get(normalizedKey);

		if (meta && now - meta.lastTriggeredAt < 55) {
			return;
		}

		const token = (meta?.token ?? 0) + 1;
		const existingTimer = pulseTimers.get(normalizedKey);

		if (existingTimer) {
			window.clearTimeout(existingTimer);
		}

		pulseMeta.set(normalizedKey, { token, lastTriggeredAt: now });
		pulseStates = {
			...pulseStates,
			[normalizedKey]: { token }
		};

		const timer = window.setTimeout(() => {
			const { [normalizedKey]: _expired, ...nextPulseStates } = pulseStates;
			pulseStates = nextPulseStates;
			pulseTimers.delete(normalizedKey);
		}, 700);

		pulseTimers.set(normalizedKey, timer);
	}

	function shouldRetriggerHeldPulse(key: string) {
		const meta = pulseMeta.get(normalizeKey(key));
		return !meta || Date.now() - meta.lastTriggeredAt >= 90;
	}

	function clearAllPulses() {
		for (const timer of pulseTimers.values()) {
			window.clearTimeout(timer);
		}

		pulseTimers.clear();
		pulseMeta.clear();
		pulseStates = {};
	}

	function isGroupCollapsed(groupKey: string) {
		return collapsedGroupKeys.includes(groupKey);
	}

	function toggleGroup(groupKey: string) {
		collapsedGroupKeys = collapsedGroupKeys.includes(groupKey)
			? collapsedGroupKeys.filter((key) => key !== groupKey)
			: [...collapsedGroupKeys, groupKey];
	}

	function getPreviewGroupId(group: PreviewGroup) {
		return group.key;
	}

	function boolSignalClass(channel: BoolPreviewChannel) {
		const base =
			'input-signal flex h-8 min-w-0 items-center gap-2 rounded-lg border px-2.5 text-left text-xs font-medium transition';
		const activeClass = channel.sustainedActive
			? 'border-emerald-300/60 bg-emerald-500/15 text-foreground shadow-[0_0_18px_rgba(16,185,129,0.18)]'
			: channel.pulseActive
				? 'border-emerald-300/45 bg-background/65 text-foreground shadow-[0_0_14px_rgba(16,185,129,0.12)]'
				: 'border-border/70 bg-background/55 text-muted-foreground hover:bg-background/70';
		return `${base} ${activeClass}`;
	}

	function boolDotClass(channel: BoolPreviewChannel) {
		return channel.visualActive
			? 'bg-emerald-300 shadow-[0_0_10px_rgba(110,231,183,0.85)]'
			: 'bg-muted-foreground/25';
	}

	function floatPercent(channel: InputChannelDefinition, value: number) {
		const min = channel.minFloatValue;
		const max = channel.maxFloatValue;

		if (typeof min !== 'number' || typeof max !== 'number' || max <= min) {
			return null;
		}

		return Math.max(0, Math.min(100, ((value - min) / (max - min)) * 100));
	}

	function formatFloatValue(channel: InputChannelDefinition, value: number) {
		if (!Number.isFinite(value)) {
			return '0';
		}

		if (channel.minFloatValue === 0 && channel.maxFloatValue === 1) {
			return `${Math.round(value * 100)}%`;
		}

		if (Math.abs(value) >= 1000) {
			return Math.round(value).toLocaleString();
		}

		if (Number.isInteger(value)) {
			return value.toString();
		}

		return value.toFixed(Math.abs(value) >= 10 ? 1 : 3);
	}

	function metadataLabel(key: string) {
		return labelFromKey(key);
	}
</script>

<Card class="border-surface-card-border bg-surface-card-alt shadow-sm backdrop-blur">
	<CardHeader class="space-y-3">
		<div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
			<div class="space-y-1">
				<CardTitle>Live input preview</CardTitle>
				{#if connected}
					<CardDescription>
						{moduleLabel} publishes {moduleDefinition?.channels.length ?? 0} channels into the lighting graph.
					</CardDescription>
				{/if}
			</div>
			<div class="flex flex-wrap gap-2">
				<Badge variant={connected ? 'default' : 'outline'}>
					{connected ? 'Connected' : 'Disconnected'}
				</Badge>
			</div>
		</div>
	</CardHeader>

	{#if connected}
		<CardContent class="space-y-4">
			<div class="flex flex-wrap gap-2 text-xs text-muted-foreground">
				<Badge variant="outline">{activeBoolCount} active</Badge>
				<Badge variant="outline">{totalBoolCount} bools</Badge>
				<Badge variant="outline">{totalFloatCount} meters</Badge>
				{#if totalColorCount > 0}
					<Badge variant="outline">{totalColorCount} colors</Badge>
				{/if}
				{#if metadataEntries.length > 0}
					<Badge variant="outline">{metadataEntries.length} metadata</Badge>
				{/if}
			</div>

			{#if previewGroups.length > 0}
				<Masonry
					items={previewGroups}
					getId={getPreviewGroupId}
					minColWidth={310}
					maxColWidth={680}
					gap={12}
					balance={false}
					duration={160}
					class="input-preview-masonry"
				>
					{#snippet children({ item: group })}
						<section class="input-preview-panel overflow-hidden rounded-xl border border-border/70 bg-background/45">
							<button
								type="button"
								class="flex w-full items-center justify-between gap-3 px-3 py-2 text-left transition hover:bg-background/45"
								aria-expanded={!isGroupCollapsed(group.key)}
								onclick={() => toggleGroup(group.key)}
							>
								<div class="min-w-0">
									<h2 class="truncate text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">
										{group.label}
									</h2>
								</div>
								<div class="flex shrink-0 items-center gap-2">
									{#if group.activeBoolCount > 0}
										<span class="rounded-full border border-emerald-400/30 bg-emerald-500/12 px-1.5 py-0.5 text-[10px] text-emerald-300">
											{group.activeBoolCount} on
										</span>
									{/if}
									<span class="rounded-full border border-border/70 bg-background/80 px-1.5 py-0.5 text-[10px] text-muted-foreground">
										{group.totalCount}
									</span>
									<ChevronDown
										class={`size-4 text-muted-foreground transition ${isGroupCollapsed(group.key) ? '' : 'rotate-180'}`}
									/>
								</div>
							</button>

							{#if !isGroupCollapsed(group.key)}
								<div class="space-y-3 border-t border-border/60 px-3 py-3">
									{#if group.boolChannels.length > 0}
										<div class="flex flex-wrap gap-2">
											{#each group.boolChannels as channel (channel.key)}
												<button
													type="button"
													class={boolSignalClass(channel)}
													aria-pressed={channel.visualActive}
													title={`${channel.label} - ${channel.key}`}
												>
													{#if channel.pulseActive}
														{#key channel.pulseToken}
															<span class="input-signal__pulse" aria-hidden="true"></span>
														{/key}
													{/if}
													<span class={`input-signal__content size-2 shrink-0 rounded-full ${boolDotClass(channel)}`}></span>
													<span class="input-signal__content min-w-0 flex-1 truncate">{channel.label}</span>
												</button>
											{/each}
										</div>
									{/if}

									{#if group.floatChannels.length > 0}
										<div class="grid gap-2 sm:grid-cols-2">
											{#each group.floatChannels as channel (channel.key)}
												<div class="rounded-lg border border-border/70 bg-background/55 px-2.5 py-2" title={`${channel.label} - ${channel.key}`}>
													<div class="flex items-center justify-between gap-3">
														<span class="min-w-0 truncate text-xs font-medium text-muted-foreground">
															{channel.label}
														</span>
														<span class="font-mono text-xs font-semibold text-foreground">
															{channel.formattedValue}
														</span>
													</div>
													{#if channel.percent !== null}
														<div class="mt-2 h-1.5 overflow-hidden rounded-full bg-muted">
															<div
																class="h-full rounded-full bg-sky-400 transition-[width]"
																style={`width: ${channel.percent}%`}
															></div>
														</div>
													{/if}
												</div>
											{/each}
										</div>
									{/if}

									{#if group.colorChannels.length > 0}
										<div class="flex flex-wrap gap-2">
											{#each group.colorChannels as channel (channel.key)}
												<div class="flex h-8 items-center gap-2 rounded-lg border border-border/70 bg-background/55 px-2.5 text-xs" title={`${channel.label} - ${channel.key}`}>
													<span
														class="size-4 rounded-full border border-black/10"
														style={`background: ${channel.currentValue ? rgb(channel.currentValue) : 'transparent'}`}
													></span>
													<span>{channel.label}</span>
												</div>
											{/each}
										</div>
									{/if}
								</div>
							{/if}
						</section>
					{/snippet}
				</Masonry>
			{:else}
				<div class="rounded-xl border border-dashed border-border/80 bg-background/50 px-4 py-10 text-center text-sm text-muted-foreground">
					No live input channels are available for this connection yet.
				</div>
			{/if}

			{#if metadataEntries.length > 0}
				<section class="rounded-xl border border-border/70 bg-background/45 px-3 py-3">
					<h2 class="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">
						Metadata
					</h2>
					<div class="mt-3 flex flex-wrap gap-2">
						{#each metadataEntries as [key, value] (key)}
							<div class="max-w-full rounded-lg border border-border/70 bg-background/55 px-2.5 py-2 text-xs" title={`${key}: ${value}`}>
								<p class="text-muted-foreground">{metadataLabel(key)}</p>
								<p class="max-w-[18rem] truncate font-medium text-foreground">{value}</p>
							</div>
						{/each}
					</div>
				</section>
			{/if}
		</CardContent>
	{:else}
		<CardContent>
			<div class="flex min-h-64 items-center justify-center rounded-xl border border-dashed border-border/80 bg-background/50 px-6 py-12 text-center">
				<p class="text-3xl font-semibold tracking-tight text-muted-foreground sm:text-4xl">
					Waiting for connection
				</p>
			</div>
		</CardContent>
	{/if}
</Card>

<style>
	:global(.input-preview-masonry) {
		align-items: flex-start;
	}

	:global(.input-preview-masonry .col) {
		align-content: flex-start;
	}

	.input-preview-panel {
		width: 100%;
	}

	.input-signal {
		isolation: isolate;
		overflow: hidden;
		position: relative;
	}

	.input-signal__pulse {
		animation: input-signal-pulse 520ms ease-out;
		border-radius: inherit;
		inset: 0;
		pointer-events: none;
		position: absolute;
		z-index: 0;
	}

	.input-signal__content {
		position: relative;
		z-index: 1;
	}

	@keyframes input-signal-pulse {
		0% {
			background: rgba(255, 255, 255, 0.85);
			box-shadow:
				0 0 0 1px rgba(255, 255, 255, 0.5) inset,
				0 0 22px rgba(110, 231, 183, 0.85);
			opacity: 0.95;
		}

		38% {
			background: rgba(16, 185, 129, 0.32);
			opacity: 0.55;
		}

		100% {
			background: rgba(16, 185, 129, 0);
			box-shadow:
				0 0 0 1px rgba(255, 255, 255, 0) inset,
				0 0 0 rgba(110, 231, 183, 0);
			opacity: 0;
		}
	}
</style>
