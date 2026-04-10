<script lang="ts">
	import { Handle, Position, type NodeProps } from '@xyflow/svelte';
	import type { NodePropertyDefinition } from '$lib/lucalights';
	import type { EditorFlowNode } from './types';

	let { id, data, selected = false }: NodeProps<EditorFlowNode> = $props();

	const interactiveClass =
		'nodrag nopan h-7 w-full rounded-md border border-border/70 bg-background/90 px-2 text-[11px] shadow-sm outline-none transition focus:border-ring focus:ring-4 focus:ring-ring/20';

	let visiblePropertyDefs = $derived.by(() => {
		if (data.typeId === 'constant.color') {
			return [];
		}

		return data.propertyDefs.filter((property) => !connectedInputIds.includes(property.key));
	});

	let connectedInputIds = $derived.by(() => {
		if (!Array.isArray(data.connectedInputIds)) {
			return [];
		}

		return data.connectedInputIds.filter((value): value is string => typeof value === 'string');
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

	function floatStepFor(property: NodePropertyDefinition) {
		const defaultValue = property.defaultValue;
		const hasFractionalBounds =
			(property.minFloatValue !== null &&
				property.minFloatValue !== undefined &&
				!Number.isInteger(property.minFloatValue)) ||
			(property.maxFloatValue !== null &&
				property.maxFloatValue !== undefined &&
				!Number.isInteger(property.maxFloatValue));
		const hasFractionalDefault =
			typeof defaultValue === 'number' && Number.isFinite(defaultValue) && !Number.isInteger(defaultValue);
		const isNormalizedRange =
			property.minFloatValue !== null &&
			property.minFloatValue !== undefined &&
			property.maxFloatValue !== null &&
			property.maxFloatValue !== undefined &&
			property.minFloatValue >= 0 &&
			property.maxFloatValue <= 1;

		return hasFractionalBounds || hasFractionalDefault || isNormalizedRange ? '0.01' : '1';
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

	function categoryHeaderTone(category: string) {
		switch (category) {
			case 'Constants':
				return 'bg-amber-500 text-white';
			case 'Graph Inputs':
				return 'bg-sky-500 text-white';
			case 'Math':
				return 'bg-cyan-600 text-white';
			case 'Logic':
				return 'bg-violet-500 text-white';
			case 'Time':
				return 'bg-orange-500 text-white';
			case 'Color':
				return 'bg-rose-500 text-white';
			case 'Outputs':
				return 'bg-emerald-500 text-white';
			default:
				return 'bg-zinc-700 text-white';
		}
	}

	function isOutputNode(typeId: string) {
		return typeId === 'output.segment-color' || typeId === 'output.segment-gradient';
	}

	type EnumOption = { value: string; label: string };

	const edgeOptions: EnumOption[] = [
		{ value: 'rising', label: 'Rising edge' },
		{ value: 'falling', label: 'Falling edge' }
	];

	const enumOptions: Record<string, Record<string, EnumOption[]>> = {
		'time.oscillator': {
			waveform: [
				{ value: 'sine', label: 'Sine' },
				{ value: 'square', label: 'Square' },
				{ value: 'triangle', label: 'Triangle' },
				{ value: 'sawtooth', label: 'Sawtooth' }
			]
		},
		'time.pulse': {
			edge: edgeOptions
		},
		'logic.compare': {
			mode: [
				{ value: 'greater', label: 'Greater than' },
				{ value: 'less', label: 'Less than' },
				{ value: 'equal', label: 'Equal' }
			]
		}
	};

	function getEnumOptions(typeId: string, propertyKey: string): EnumOption[] | undefined {
		return enumOptions[typeId]?.[propertyKey];
	}

	function nodeTooltip() {
		return data.description.trim().length > 0
			? `${data.category}\n${data.description}`
			: data.category;
	}

	function propertyTooltip(property: NodePropertyDefinition) {
		return property.description.trim().length > 0
			? `${property.label} · ${property.valueType}\n${property.description}`
			: `${property.label} · ${property.valueType}`;
	}

	function portTooltip(label: string, valueType: string, description: string) {
		return description.trim().length > 0
			? `${label} · ${valueType}\n${description}`
			: `${label} · ${valueType}`;
	}
</script>

<div
	class={`w-[15.5rem] overflow-visible rounded-[1.05rem] border bg-surface-node text-left shadow-lg backdrop-blur ${
		selected ? 'border-primary ring-2 ring-primary/20' : 'border-border/70'
	}`}
>
	<div class={`flex items-center justify-between gap-3 rounded-t-[1.05rem] border-b border-black/10 px-3 py-2 ${categoryHeaderTone(data.category)}`}>
		<h3 class="min-w-0 flex-1 truncate text-[13px] font-semibold tracking-tight" title={nodeTooltip()}>
			{data.label}
		</h3>
		<span class="rounded-full border border-white/15 bg-black/10 px-1.5 py-0.5 font-mono text-[10px] text-white/80">
				{data.inputs.length}:{data.outputs.length}
		</span>
	</div>

	{#if data.inputs.length > 0}
		<div class="border-b border-border/60 px-0 py-1">
			{#each data.inputs as input}
				<div
					class="relative flex min-h-7 items-center px-3 pl-4 text-foreground/90 transition hover:bg-surface-subtle-hover"
					title={portTooltip(input.label, input.valueType, input.description)}
				>
					<Handle
						type="target"
						id={input.id}
						position={Position.Left}
						style="left: 0; top: 50%; transform: translate(-50%, -50%);"
						class={`${handleTone(input.valueType)} !h-4 !w-4 !border-[3px] !border-node-handle-border !shadow-sm`}
					/>
					<p class="min-w-0 flex-1 truncate text-[12px] font-medium">{input.label}</p>
				</div>
			{/each}
		</div>
	{/if}

	<div class="space-y-2.5 px-3 py-2.5">
		{#if data.typeId === 'constant.color'}
			<div class="space-y-1.5">
				<div class="flex items-center justify-between gap-3">
					<p
						class="text-[10px] font-semibold uppercase tracking-[0.14em] text-muted-foreground"
						title="Color · Color&#10;Outputs a fixed RGB color."
					>
						Color
					</p>
					<span class="font-mono text-[11px] text-muted-foreground">{colorHex}</span>
				</div>

				<div class="flex items-center gap-2">
					<input
						class="nodrag nopan h-9 w-10 cursor-pointer rounded-md border border-border/70 bg-transparent p-1"
						type="color"
						value={colorHex}
						oninput={(event) => setColorFromHex((event.currentTarget as HTMLInputElement).value)}
					/>
					<div class="grid flex-1 grid-cols-3 gap-1.5">
						<label class="space-y-1">
							<span class="px-0.5 text-[10px] uppercase tracking-[0.12em] text-muted-foreground">R</span>
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
							<span class="px-0.5 text-[10px] uppercase tracking-[0.12em] text-muted-foreground">G</span>
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
							<span class="px-0.5 text-[10px] uppercase tracking-[0.12em] text-muted-foreground">B</span>
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
			<div class="space-y-1.5">
				<div class="flex items-center justify-between gap-2">
					<p
						class="text-[10px] font-semibold uppercase tracking-[0.14em] text-muted-foreground"
						title={propertyTooltip(property)}
					>
						{property.label}
					</p>
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
				{:else if getEnumOptions(data.typeId, property.key)}
					<select
						class={interactiveClass}
						value={stringValue(property.key, String(valueFor(property) ?? ''))}
						onchange={(event) => setProperty(property.key, (event.currentTarget as HTMLSelectElement).value)}
					>
						{#each getEnumOptions(data.typeId, property.key) ?? [] as option}
							<option value={option.value}>{option.label}</option>
						{/each}
					</select>
				{:else if property.valueType === 'Bool'}
					<label class="flex items-center gap-2 rounded-md border border-border/70 bg-background/80 px-2.5 py-1.5 text-[11px] font-medium">
						<input
							class="nodrag nopan size-4 rounded border-border text-primary"
							type="checkbox"
							checked={boolValue(property.key, Boolean(valueFor(property)))}
							onchange={(event) => setProperty(property.key, (event.currentTarget as HTMLInputElement).checked)}
						/>
						Enabled
					</label>
				{:else if property.valueType === 'Float'}
					<div class="space-y-1.5">
						<input
							class={interactiveClass}
							type="number"
							min={property.minFloatValue ?? undefined}
							max={property.maxFloatValue ?? undefined}
							step={floatStepFor(property)}
							value={numberValue(property.key, Number(valueFor(property) ?? 0))}
							oninput={(event) => setProperty(property.key, Number((event.currentTarget as HTMLInputElement).value))}
						/>

						{#if property.minFloatValue !== null && property.minFloatValue !== undefined && property.maxFloatValue !== null && property.maxFloatValue !== undefined}
							<input
								class="nodrag nopan h-1.5 w-full cursor-pointer appearance-none rounded-full bg-muted accent-primary"
								type="range"
								min={property.minFloatValue}
								max={property.maxFloatValue}
								step={floatStepFor(property)}
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

				{#if isOutputNode(data.typeId) && property.key === 'deviceIds' && data.deviceOptions.length > 0}
					<div class="flex flex-wrap gap-1">
						{#each data.deviceOptions as device}
							<button
								type="button"
								class={`nodrag nopan rounded-full border px-1.5 py-0.5 text-[9px] transition ${
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

				{#if isOutputNode(data.typeId) && property.key === 'segmentIds' && data.segmentOptions.length > 0}
					<div class="flex flex-wrap gap-1">
						{#each data.segmentOptions as segment}
							<button
								type="button"
								class={`nodrag nopan rounded-full border px-1.5 py-0.5 text-[9px] transition ${
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

				{#if isOutputNode(data.typeId) && property.key === 'groupIds' && data.groupOptions.length > 0}
					<div class="flex flex-wrap gap-1">
						{#each data.groupOptions as groupId}
							<button
								type="button"
								class={`nodrag nopan rounded-full border px-1.5 py-0.5 text-[9px] transition ${
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
		<div class="border-t border-border/60 px-0 py-1">
			{#each data.outputs as output}
				<div
					class="relative flex min-h-7 items-center justify-end px-3 pr-4 text-right text-foreground/90 transition hover:bg-surface-subtle-hover"
					title={portTooltip(output.label, output.valueType, output.description)}
				>
					<p class="min-w-0 flex-1 truncate text-[12px] font-medium">{output.label}</p>
					<Handle
						type="source"
						id={output.id}
						position={Position.Right}
						style="right: 0; top: 50%; transform: translate(50%, -50%);"
						class={`${handleTone(output.valueType)} !h-4 !w-4 !border-[3px] !border-node-handle-border !shadow-sm`}
					/>
				</div>
			{/each}
		</div>
	{/if}
</div>
