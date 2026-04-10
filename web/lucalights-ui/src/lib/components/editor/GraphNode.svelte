<script lang="ts">
	import { Handle, Position, type NodeProps } from '@xyflow/svelte';
	import type { NodePropertyDefinition } from '$lib/lucalights';
	import type { EditorFlowNode } from './types';

	let { id, data, selected = false }: NodeProps<EditorFlowNode> = $props();

	const interactiveClass =
		'nodrag nopan h-8 w-full rounded-lg border border-border/70 bg-background/90 px-2.5 text-xs shadow-sm outline-none transition focus:border-ring focus:ring-4 focus:ring-ring/20';

	let visiblePropertyDefs = $derived.by(() => {
		if (data.typeId === 'constant.color') {
			return [];
		}

		return data.propertyDefs;
	});

	let inputKeyOptions = $derived.by(() => {
		if (!data.typeId.startsWith('input.')) {
			return [];
		}

		const valueType = data.typeId === 'input.bool'
			? 'Bool'
			: data.typeId === 'input.float'
				? 'Float'
				: 'Color';

		return data.inputChannelOptions.filter((channel) => normalizeInputValueType(channel.valueType) === valueType);
	});

	let colorHex = $derived.by(() => {
		const red = byteValue('r', 255);
		const green = byteValue('g', 255);
		const blue = byteValue('b', 255);
		return `#${toHex(red)}${toHex(green)}${toHex(blue)}`;
	});

	function toHex(value: number) {
		return Math.max(0, Math.min(255, value)).toString(16).padStart(2, '0');
	}

	function valueFor(property: NodePropertyDefinition): unknown {
		return data.properties[property.key] ?? property.defaultValue ?? defaultValueForType(property.valueType);
	}

	function defaultValueForType(valueType: string): unknown {
		switch (valueType) {
			case 'Bool':
				return false;
			case 'Float':
				return 0;
			default:
				return '';
		}
	}

	function stringValue(key: string, fallback = '') {
		const value = data.properties[key];
		if (typeof value === 'string') {
			return value;
		}

		return fallback;
	}

	function numberValue(key: string, fallback = 0) {
		const value = data.properties[key];
		if (typeof value === 'number' && Number.isFinite(value)) {
			return value;
		}

		if (typeof value === 'string') {
			const parsed = Number(value);
			if (Number.isFinite(parsed)) {
				return parsed;
			}
		}

		return fallback;
	}

	function byteValue(key: string, fallback = 0) {
		return Math.max(0, Math.min(255, Math.round(numberValue(key, fallback))));
	}

	function boolValue(key: string, fallback = false) {
		const value = data.properties[key];
		if (typeof value === 'boolean') {
			return value;
		}

		if (typeof value === 'string') {
			return value.trim().toLowerCase() === 'true';
		}

		return fallback;
	}

	function setProperty(key: string, value: unknown) {
		data.onPropertyChange?.(id, key, value);
	}

	function setColorFromHex(hex: string) {
		const normalized = hex.replace('#', '');
		if (normalized.length !== 6) {
			return;
		}

		setProperty('r', parseInt(normalized.slice(0, 2), 16));
		setProperty('g', parseInt(normalized.slice(2, 4), 16));
		setProperty('b', parseInt(normalized.slice(4, 6), 16));
	}

	function csvValues(key: string) {
		return stringValue(key)
			.split(',')
			.map((value) => value.trim())
			.filter((value) => value.length > 0);
	}

	function csvContains(key: string, value: string) {
		return csvValues(key).some((entry) => entry.toLowerCase() === value.toLowerCase());
	}

	function toggleCsvValue(key: string, value: string) {
		const nextValues = csvValues(key);
		const matchIndex = nextValues.findIndex((entry) => entry.toLowerCase() === value.toLowerCase());

		if (matchIndex >= 0) {
			nextValues.splice(matchIndex, 1);
		} else {
			nextValues.push(value);
		}

		setProperty(key, nextValues.join(', '));
	}

	function handleTone(valueType: string) {
		switch (valueType) {
			case 'Bool':
				return '!bg-amber-500 !border-amber-500';
			case 'Float':
				return '!bg-sky-500 !border-sky-500';
			case 'Color':
				return '!bg-rose-500 !border-rose-500';
			case 'String':
				return '!bg-emerald-500 !border-emerald-500';
			default:
				return '!bg-zinc-500 !border-zinc-500';
		}
	}

	function normalizeInputValueType(valueType: unknown): string {
		if (typeof valueType === 'string') {
			return valueType;
		}

		switch (valueType) {
			case 0:
				return 'Bool';
			case 1:
				return 'Float';
			case 2:
				return 'Color';
			case 3:
				return 'String';
			default:
				return '';
		}
	}

	function categoryTone(category: string) {
		switch (category) {
			case 'Constants':
				return 'bg-amber-100 text-amber-900';
			case 'Graph Inputs':
				return 'bg-sky-100 text-sky-900';
			case 'Logic':
				return 'bg-violet-100 text-violet-900';
			case 'Outputs':
				return 'bg-emerald-100 text-emerald-900';
			default:
				return 'bg-zinc-200 text-zinc-800';
		}
	}
</script>

<div
	class={`min-w-[17rem] overflow-visible rounded-[1.35rem] border bg-white/96 text-left shadow-lg backdrop-blur ${
		selected ? 'border-primary ring-4 ring-primary/15' : 'border-border/70'
	}`}
>
	<div class="space-y-2 rounded-t-[1.35rem] border-b border-border/70 bg-[linear-gradient(180deg,rgba(255,255,255,0.95),rgba(245,241,235,0.9))] px-4 py-3">
		<div class="flex items-start justify-between gap-3">
			<div class="space-y-1">
				<p class={`inline-flex rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase tracking-[0.22em] ${categoryTone(data.category)}`}>
					{data.category}
				</p>
				<h3 class="text-sm font-semibold tracking-tight">{data.label}</h3>
			</div>
			<span class="rounded-full bg-black/5 px-2 py-1 text-[10px] uppercase tracking-[0.18em] text-muted-foreground">
				{data.inputs.length}:{data.outputs.length}
			</span>
		</div>

		<p class="text-[11px] leading-5 text-muted-foreground">{data.description}</p>
	</div>

	{#if data.inputs.length > 0}
		<div class="space-y-1.5 border-b border-border/70 px-3 py-3">
			<p class="px-1 text-[10px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
				Inputs
			</p>
			{#each data.inputs as input}
				<div class="relative flex items-center gap-2 rounded-xl border border-border/70 bg-background/80 px-3 py-2 pl-5">
					<Handle
						type="target"
						id={input.id}
						position={Position.Left}
						style="top: 50%; transform: translateY(-50%);"
						class={handleTone(input.valueType)}
					/>
					<div class="min-w-0 flex-1">
						<p class="truncate text-xs font-medium">{input.label}</p>
						<p class="truncate text-[10px] uppercase tracking-[0.16em] text-muted-foreground">
							{input.valueType}
						</p>
					</div>
				</div>
			{/each}
		</div>
	{/if}

	<div class="space-y-3 px-3 py-3">
		{#if data.typeId === 'constant.color'}
			<div class="space-y-2">
				<div class="flex items-center justify-between gap-3">
					<p class="text-[10px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
						Color
					</p>
					<span class="font-mono text-[11px] text-muted-foreground">{colorHex}</span>
				</div>

				<div class="flex items-center gap-3">
					<input
						class="nodrag nopan h-10 w-12 cursor-pointer rounded-xl border border-border/70 bg-transparent p-1"
						type="color"
						value={colorHex}
						oninput={(event) => setColorFromHex((event.currentTarget as HTMLInputElement).value)}
					/>
					<div class="grid flex-1 grid-cols-3 gap-2">
						<label class="space-y-1">
							<span class="px-1 text-[10px] uppercase tracking-[0.14em] text-muted-foreground">R</span>
							<input
								class={interactiveClass}
								type="number"
								min="0"
								max="255"
								value={byteValue('r', 255)}
								oninput={(event) => setProperty('r', Number((event.currentTarget as HTMLInputElement).value))}
							/>
						</label>
						<label class="space-y-1">
							<span class="px-1 text-[10px] uppercase tracking-[0.14em] text-muted-foreground">G</span>
							<input
								class={interactiveClass}
								type="number"
								min="0"
								max="255"
								value={byteValue('g', 255)}
								oninput={(event) => setProperty('g', Number((event.currentTarget as HTMLInputElement).value))}
							/>
						</label>
						<label class="space-y-1">
							<span class="px-1 text-[10px] uppercase tracking-[0.14em] text-muted-foreground">B</span>
							<input
								class={interactiveClass}
								type="number"
								min="0"
								max="255"
								value={byteValue('b', 255)}
								oninput={(event) => setProperty('b', Number((event.currentTarget as HTMLInputElement).value))}
							/>
						</label>
					</div>
				</div>
			</div>
		{/if}

		{#each visiblePropertyDefs as property}
			<div class="space-y-2">
				<div class="space-y-1">
					<p class="text-[10px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
						{property.label}
					</p>
					<p class="text-[11px] leading-4 text-muted-foreground">{property.description}</p>
				</div>

				{#if data.typeId.startsWith('input.') && property.key === 'key'}
					<select
						class={interactiveClass}
						value={stringValue(property.key, String(valueFor(property) ?? ''))}
						onchange={(event) => setProperty(property.key, (event.currentTarget as HTMLSelectElement).value)}
					>
						<option value="">Select channel</option>
						{#each inputKeyOptions as option}
							<option value={option.key}>{option.label} · {option.key}</option>
						{/each}
					</select>
				{:else if property.valueType === 'Bool'}
					<label class="flex items-center gap-2 rounded-xl border border-border/70 bg-background/80 px-3 py-2 text-xs font-medium">
						<input
							class="nodrag nopan size-4 rounded border-border text-primary"
							type="checkbox"
							checked={boolValue(property.key, Boolean(valueFor(property)))}
							onchange={(event) => setProperty(property.key, (event.currentTarget as HTMLInputElement).checked)}
						/>
						Enabled
					</label>
				{:else if property.valueType === 'Float'}
					<div class="space-y-2">
						<input
							class={interactiveClass}
							type="number"
							min={property.minFloatValue ?? undefined}
							max={property.maxFloatValue ?? undefined}
							step="1"
							value={numberValue(property.key, Number(valueFor(property) ?? 0))}
							oninput={(event) => setProperty(property.key, Number((event.currentTarget as HTMLInputElement).value))}
						/>

						{#if property.minFloatValue !== null && property.minFloatValue !== undefined && property.maxFloatValue !== null && property.maxFloatValue !== undefined}
							<input
								class="nodrag nopan h-2 w-full cursor-pointer appearance-none rounded-full bg-muted accent-primary"
								type="range"
								min={property.minFloatValue}
								max={property.maxFloatValue}
								step="1"
								value={numberValue(property.key, Number(valueFor(property) ?? 0))}
								oninput={(event) => setProperty(property.key, Number((event.currentTarget as HTMLInputElement).value))}
							/>
						{/if}
					</div>
				{:else}
					<input
						class={interactiveClass}
						type="text"
						value={stringValue(property.key, String(valueFor(property) ?? ''))}
						oninput={(event) => setProperty(property.key, (event.currentTarget as HTMLInputElement).value)}
					/>
				{/if}

				{#if data.typeId === 'output.segment-color' && property.key === 'deviceIds' && data.deviceOptions.length > 0}
					<div class="flex flex-wrap gap-1.5">
						{#each data.deviceOptions as device}
							<button
								type="button"
								class={`nodrag nopan rounded-full border px-2 py-1 text-[10px] transition ${
									csvContains(property.key, device.id)
										? 'border-primary bg-primary/10 text-primary'
										: 'border-border/70 bg-background/70 text-muted-foreground'
								}`}
								onclick={() => toggleCsvValue(property.key, device.id)}
							>
								{device.name}
							</button>
						{/each}
					</div>
				{/if}

				{#if data.typeId === 'output.segment-color' && property.key === 'segmentIds' && data.segmentOptions.length > 0}
					<div class="flex flex-wrap gap-1.5">
						{#each data.segmentOptions as segment}
							<button
								type="button"
								class={`nodrag nopan rounded-full border px-2 py-1 text-[10px] transition ${
									csvContains(property.key, segment.id)
										? 'border-primary bg-primary/10 text-primary'
										: 'border-border/70 bg-background/70 text-muted-foreground'
								}`}
								title={`${segment.deviceName} · ${segment.id}`}
								onclick={() => toggleCsvValue(property.key, segment.id)}
							>
								{segment.name}
							</button>
						{/each}
					</div>
				{/if}

				{#if data.typeId === 'output.segment-color' && property.key === 'groupIds' && data.groupOptions.length > 0}
					<div class="flex flex-wrap gap-1.5">
						{#each data.groupOptions as groupId}
							<button
								type="button"
								class={`nodrag nopan rounded-full border px-2 py-1 text-[10px] transition ${
									csvContains(property.key, String(groupId))
										? 'border-primary bg-primary/10 text-primary'
										: 'border-border/70 bg-background/70 text-muted-foreground'
								}`}
								onclick={() => toggleCsvValue(property.key, String(groupId))}
							>
								Group {groupId}
							</button>
						{/each}
					</div>
				{/if}
			</div>
		{/each}
	</div>

	{#if data.outputs.length > 0}
		<div class="space-y-1.5 border-t border-border/70 px-3 py-3">
			<p class="px-1 text-[10px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
				Outputs
			</p>
			{#each data.outputs as output}
				<div class="relative flex items-center gap-2 rounded-xl border border-border/70 bg-background/80 px-3 py-2 pr-5">
					<div class="min-w-0 flex-1">
						<p class="truncate text-xs font-medium">{output.label}</p>
						<p class="truncate text-[10px] uppercase tracking-[0.16em] text-muted-foreground">
							{output.valueType}
						</p>
					</div>
					<Handle
						type="source"
						id={output.id}
						position={Position.Right}
						style="top: 50%; transform: translateY(-50%);"
						class={handleTone(output.valueType)}
					/>
				</div>
			{/each}
		</div>
	{/if}
</div>
