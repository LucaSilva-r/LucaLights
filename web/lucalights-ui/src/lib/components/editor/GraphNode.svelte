<script lang="ts">
	import { Handle, Position, type NodeProps } from '@xyflow/svelte';
	import type { ColorValue, NodePortDefinition, NodePropertyDefinition } from '$lib/lucalights';
	import InputChannelPicker from './InputChannelPicker.svelte';
	import NodeColorPicker from './NodeColorPicker.svelte';
	import NodeNumberInput from './NodeNumberInput.svelte';
	import OutputTargetPicker from './OutputTargetPicker.svelte';
	import GradientEditor, { type GradientStop } from './GradientEditor.svelte';
	import type { EditorFlowNode } from './types';

	let { id, data, selected = false }: NodeProps<EditorFlowNode> = $props();

	const inlineInputClass =
		'nodrag nopan h-7 min-w-0 flex-1 rounded-md border border-border/70 bg-background/90 px-2 text-[11px] text-right shadow-sm outline-none transition focus:border-ring focus:ring-4 focus:ring-ring/20';
	const outputTargetKeys = new Set(['segmentIds']);

	let connectedInputIds = $derived.by(() => {
		if (!Array.isArray(data.connectedInputIds)) {
			return [];
		}

		return data.connectedInputIds.filter((value): value is string => typeof value === 'string');
	});

	let propertyMap = $derived(new Map(data.propertyDefs.map((property) => [property.key, property])));

	let standaloneProperties = $derived.by(() => {
		const inputIds = new Set(data.inputs.map((input) => input.id));
		const outputIds = new Set(data.outputs.map((output) => output.id));
		return data.propertyDefs.filter(
			(property) =>
				!inputIds.has(property.key) &&
				!outputIds.has(property.key) &&
				!(isOutputNode(data.typeId) && outputTargetKeys.has(property.key)) &&
				!connectedInputIds.includes(property.key)
		);
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

	function floatPrecisionFor(property: NodePropertyDefinition) {
		return floatStepFor(property) === '1' ? 0 : 3;
	}

	function hasRange(property: NodePropertyDefinition) {
		return (
			property.minFloatValue !== null &&
			property.minFloatValue !== undefined &&
			property.maxFloatValue !== null &&
			property.maxFloatValue !== undefined
		);
	}

	function sliderPercent(property: NodePropertyDefinition) {
		const min = property.minFloatValue ?? 0;
		const max = property.maxFloatValue ?? 1;
		const value = numberValue(property.key, Number(valueFor(property) ?? 0));
		if (max === min) return 0;
		return Math.max(0, Math.min(100, ((value - min) / (max - min)) * 100));
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

	function propertyColorValue(property: NodePropertyDefinition): ColorValue {
		const raw = data.properties[property.key] ?? property.defaultValue;
		if (
			raw &&
			typeof raw === 'object' &&
			'r' in raw &&
			'g' in raw &&
			'b' in raw
		) {
			const color = raw as { r?: unknown; g?: unknown; b?: unknown };
			return {
				r: Math.max(0, Math.min(255, Number(color.r ?? 255))),
				g: Math.max(0, Math.min(255, Number(color.g ?? 255))),
				b: Math.max(0, Math.min(255, Number(color.b ?? 255)))
			};
		}

		return { r: 255, g: 255, b: 255 };
	}

	function propertyColorHex(property: NodePropertyDefinition) {
		const color = propertyColorValue(property);
		return `#${toHex(color.r)}${toHex(color.g)}${toHex(color.b)}`;
	}

	function setColorPropertyFromHex(key: string, hex: string) {
		const normalized = hex.replace('#', '');
		if (normalized.length !== 6) {
			return;
		}

		setProperty(key, {
			r: parseInt(normalized.slice(0, 2), 16),
			g: parseInt(normalized.slice(2, 4), 16),
			b: parseInt(normalized.slice(4, 6), 16)
		});
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
			case 'Annotations':
				return 'bg-amber-200 text-amber-950 dark:bg-amber-500/25 dark:text-amber-100';
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
			case 'Segment':
				return 'bg-teal-500 text-white';
			case 'Outputs':
				return 'bg-emerald-500 text-white';
			default:
				return 'bg-zinc-700 text-white';
		}
	}

	function isOutputNode(typeId: string) {
		return typeId === 'output.segment-color';
	}

	function isGradientNode(typeId: string) {
		return typeId === 'color.gradient';
	}

	function parseGradientStops(json: string): GradientStop[] {
		try {
			const arr = JSON.parse(json);
			if (Array.isArray(arr) && arr.length >= 1) {
				return arr.map((s: Record<string, number>) => ({
					p: s.p ?? 0,
					r: s.r ?? 0,
					g: s.g ?? 0,
					b: s.b ?? 0
				}));
			}
		} catch { /* fallthrough */ }
		return [
			{ p: 0, r: 0, g: 0, b: 0 },
			{ p: 1, r: 255, g: 255, b: 255 }
		];
	}

	function serializeGradientStops(stops: GradientStop[]): string {
		return JSON.stringify(stops.map((s) => ({ p: s.p, r: s.r, g: s.g, b: s.b })));
	}

	function isCommentNode(typeId: string) {
		return typeId === 'annotation.comment';
	}

	function commentProperty(key: string) {
		return propertyMap.get(key);
	}

	type EnumOption = { value: string; label: string };

	const edgeOptions: EnumOption[] = [
		{ value: 'rising', label: 'Rising edge' },
		{ value: 'falling', label: 'Falling edge' }
	];

	const enumOptions: Record<string, Record<string, EnumOption[]>> = {
		'input.bool': {
			mergeMode: [
				{ value: 'any', label: 'Any selected' },
				{ value: 'all', label: 'All selected' }
			]
		},
		'input.float': {
			mergeMode: [
				{ value: 'max', label: 'Highest value' },
				{ value: 'min', label: 'Lowest value' },
				{ value: 'average', label: 'Average value' }
			]
		},
		'input.color': {
			mergeMode: [
				{ value: 'average', label: 'Average colors' },
				{ value: 'additive', label: 'Additive blend' }
			]
		},
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
		},
		'color.gradient': {
			interpolation: [
				{ value: 'linear', label: 'Linear' },
				{ value: 'constant', label: 'Constant' }
			]
		},
		'logic.mix-color': {
			mode: [
				{ value: 'mix', label: 'Mix' },
				{ value: 'darken', label: 'Darken' },
				{ value: 'multiply', label: 'Multiply' },
				{ value: 'color-burn', label: 'Color Burn' },
				{ value: 'lighten', label: 'Lighten' },
				{ value: 'screen', label: 'Screen' },
				{ value: 'color-dodge', label: 'Color Dodge' },
				{ value: 'add', label: 'Add' },
				{ value: 'overlay', label: 'Overlay' },
				{ value: 'soft-light', label: 'Soft Light' },
				{ value: 'linear-light', label: 'Linear Light' },
				{ value: 'difference', label: 'Difference' },
				{ value: 'exclusion', label: 'Exclusion' },
				{ value: 'subtract', label: 'Subtract' },
				{ value: 'divide', label: 'Divide' },
				{ value: 'hue', label: 'Hue' },
				{ value: 'saturation', label: 'Saturation' },
				{ value: 'color', label: 'Color' },
				{ value: 'value', label: 'Value' }
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

	function inputIsConnected(inputId: string) {
		return connectedInputIds.includes(inputId);
	}

	function isColorInput(input: NodePortDefinition) {
		return data.typeId === 'constant.color' && input.valueType === 'Color';
	}

	function isConstantColorNode(typeId: string) {
		return typeId === 'constant.color';
	}

	function handleSliderPointer(event: PointerEvent, property: NodePropertyDefinition) {
		const target = event.currentTarget as HTMLElement;
		target.setPointerCapture(event.pointerId);

		const min = property.minFloatValue ?? 0;
		const max = property.maxFloatValue ?? 1;
		const step = Number(floatStepFor(property));

		function update(clientX: number) {
			const rect = target.getBoundingClientRect();
			const ratio = Math.max(0, Math.min(1, (clientX - rect.left) / rect.width));
			const raw = min + ratio * (max - min);
			const snapped = Math.round(raw / step) * step;
			setProperty(property.key, Math.max(min, Math.min(max, Number(snapped.toFixed(6)))));
		}

		update(event.clientX);

		function onMove(moveEvent: PointerEvent) {
			update(moveEvent.clientX);
		}

		function onUp() {
			target.removeEventListener('pointermove', onMove);
			target.removeEventListener('pointerup', onUp);
		}

		target.addEventListener('pointermove', onMove);
		target.addEventListener('pointerup', onUp);
	}

	function formatSliderValue(property: NodePropertyDefinition) {
		const value = numberValue(property.key, Number(valueFor(property) ?? 0));
		const step = floatStepFor(property);
		return step === '0.01' ? value.toFixed(3) : String(value);
	}
</script>

{#snippet sliderField(property: NodePropertyDefinition, label: string)}
	<!-- Blender-style slider: label left, value right, fill bar behind -->
	<div
		class="nodrag nopan relative flex h-7 cursor-ew-resize select-none items-center overflow-hidden rounded-md border border-border/70 bg-background/90 shadow-sm"
		role="slider"
		aria-label={label}
		aria-valuemin={property.minFloatValue ?? 0}
		aria-valuemax={property.maxFloatValue ?? 1}
		aria-valuenow={numberValue(property.key, Number(valueFor(property) ?? 0))}
		tabindex={0}
		title={propertyTooltip(property)}
		onpointerdown={(event) => handleSliderPointer(event, property)}
	>
		<div
			class="pointer-events-none absolute inset-y-0 left-0 bg-primary/25"
			style="width: {sliderPercent(property)}%"
		></div>
		<span class="relative z-10 px-2 text-[12px] font-medium">{label}</span>
		<span class="relative z-10 ml-auto px-2 text-[11px] tabular-nums text-muted-foreground">{formatSliderValue(property)}</span>
	</div>
{/snippet}

{#snippet inlineEditor(property: NodePropertyDefinition, label: string)}
	{#if data.typeId.startsWith('input.') && property.key === 'key'}
		<InputChannelPicker
			value={stringValue(property.key, String(valueFor(property) ?? ''))}
			channels={inputKeyOptions}
			onChange={(nextValue) => setProperty(property.key, nextValue)}
		/>
	{:else if getEnumOptions(data.typeId, property.key)}
		<select
			class={inlineInputClass}
			value={stringValue(property.key, String(valueFor(property) ?? ''))}
			onchange={(event) => setProperty(property.key, (event.currentTarget as HTMLSelectElement).value)}
		>
			{#each getEnumOptions(data.typeId, property.key) ?? [] as option}
				<option value={option.value}>{option.label}</option>
			{/each}
		</select>
	{:else if property.valueType === 'Bool'}
		<input
			class="nodrag nopan size-4 rounded border-border text-primary"
			type="checkbox"
			checked={boolValue(property.key, Boolean(valueFor(property)))}
			onchange={(event) => setProperty(property.key, (event.currentTarget as HTMLInputElement).checked)}
		/>
	{:else if property.valueType === 'Float' && hasRange(property)}
		{@render sliderField(property, label)}
	{:else if property.valueType === 'Float'}
		<NodeNumberInput
			className="flex-1"
			value={numberValue(property.key, Number(valueFor(property) ?? 0))}
			step={Number(floatStepFor(property))}
			precision={floatPrecisionFor(property)}
			onchange={(next) => setProperty(property.key, next)}
		/>
	{:else if property.valueType === 'Color'}
		<NodeColorPicker
			hex={propertyColorHex(property)}
			label={label}
			onchange={(hex) => setColorPropertyFromHex(property.key, hex)}
		/>
	{:else}
		<input
			class={inlineInputClass}
			type="text"
			value={stringValue(property.key, String(valueFor(property) ?? ''))}
			oninput={(event) => setProperty(property.key, (event.currentTarget as HTMLInputElement).value)}
		/>
	{/if}
{/snippet}

<div
	class={`overflow-visible rounded-[1.05rem] border text-left shadow-lg backdrop-blur ${
		isCommentNode(data.typeId)
			? 'w-[19rem] bg-surface-card text-foreground'
			: 'w-[15.5rem] bg-surface-node'
	} ${
		selected
			? isCommentNode(data.typeId)
				? 'border-amber-300 ring-2 ring-amber-200/50 dark:border-amber-500/40 dark:ring-amber-500/20'
				: 'border-primary ring-2 ring-primary/20'
			: isCommentNode(data.typeId)
				? 'border-amber-200/80 dark:border-amber-500/25'
				: 'border-border/70'
	}`}
>
	<!-- Header -->
	<div class={`flex items-center justify-between gap-3 rounded-t-[1.05rem] border-b border-black/10 px-3 py-2 ${categoryHeaderTone(data.category)}`}>
		<h3 class="min-w-0 flex-1 truncate text-[13px] font-semibold tracking-tight" title={nodeTooltip()}>
			{data.label}
		</h3>
		{#if !isCommentNode(data.typeId)}
			<span class="rounded-full border border-white/15 bg-black/10 px-1.5 py-0.5 font-mono text-[10px] text-white/80">
					{data.inputs.length}:{data.outputs.length}
			</span>
		{/if}
	</div>

	<!-- Outputs (top, right-aligned, inline editor when property exists) -->
	{#if data.outputs.length > 0}
		<div class="border-b border-border/60 py-1">
			{#each data.outputs as output}
				{@const property = propertyMap.get(output.id)}
				{@const editableOutputProperty = data.inputs.length === 0 ? property : undefined}
				<div
					class="relative flex min-h-7 items-center gap-2 px-3 pr-4 text-foreground/90 transition hover:bg-surface-subtle-hover"
					title={portTooltip(output.label, output.valueType, output.description)}
				>
					{#if editableOutputProperty}
						{@render inlineEditor(editableOutputProperty, output.label)}
					{:else}
						<p class="min-w-0 flex-1 truncate text-right text-[13px] font-medium">{output.label}</p>
					{/if}
					<Handle
						type="source"
						id={output.id}
						position={Position.Right}
						style="right: 0; top: 50%; transform: translate(50%, -50%);"
						class={`${handleTone(output.valueType)} !h-2.5 !w-2.5 !border-0 !shadow-sm`}
					/>
				</div>
			{/each}
		</div>
	{/if}

	<!-- Inputs (inline: handle + label/editor on same row) -->
	{#if data.inputs.length > 0}
		<div class="py-1">
			{#each data.inputs as input}
				{@const connected = inputIsConnected(input.id)}
				{@const property = propertyMap.get(input.id)}
				{@const showEditor = !connected && property}
				{@const isColor = isColorInput(input)}
				{@const isPropertyColor = property?.valueType === 'Color'}

				<div
					class="relative py-0.5 text-foreground/90 transition hover:bg-surface-subtle-hover"
					title={portTooltip(input.label, input.valueType, input.description)}
				>
					{#if (showEditor && isPropertyColor) || isColor}
						<!-- Color input: click-to-open color picker -->
						<div class="px-3 pl-4">
							<div class="relative">
								<Handle
									type="target"
									id={input.id}
									position={Position.Left}
									style="left: -12px; top: 14px; transform: translate(-50%, -50%);"
									class={`${handleTone(input.valueType)} !h-2.5 !w-2.5 !border-0 !shadow-sm`}
								/>
								<NodeColorPicker
									hex={isColor ? colorHex : propertyColorHex(property!)}
									label={input.label}
									onchange={(hex) => isColor ? setColorFromHex(hex) : setColorPropertyFromHex(property!.key, hex)}
								/>
							</div>
						</div>
					{:else if showEditor && property.valueType === 'Float' && hasRange(property)}
						<!-- Float with range: Blender-style slider fills the whole row -->
						<div class="flex min-h-7 items-center pl-4 pr-3">
							<Handle
								type="target"
								id={input.id}
								position={Position.Left}
								style="left: 0; top: 50%; transform: translate(-50%, -50%);"
								class={`${handleTone(input.valueType)} !h-2.5 !w-2.5 !border-0 !shadow-sm`}
							/>
							<div class="min-w-0 flex-1">
								{@render sliderField(property, input.label)}
							</div>
						</div>
					{:else if showEditor}
						<!-- Other editors: label + inline input on same row -->
						<div class="flex min-h-7 items-center gap-2 px-3 pl-4">
							<Handle
								type="target"
								id={input.id}
								position={Position.Left}
								style="left: 0; top: 50%; transform: translate(-50%, -50%);"
								class={`${handleTone(input.valueType)} !h-2.5 !w-2.5 !border-0 !shadow-sm`}
							/>
							<span class="shrink-0 text-[13px] font-medium">{input.label}</span>
							{@render inlineEditor(property, input.label)}
						</div>
					{:else}
						<!-- Connected or no matching property: just the label -->
						<div class="flex min-h-7 items-center px-3 pl-4">
							<Handle
								type="target"
								id={input.id}
								position={Position.Left}
								style="left: 0; top: 50%; transform: translate(-50%, -50%);"
								class={`${handleTone(input.valueType)} !h-2.5 !w-2.5 !border-0 !shadow-sm`}
							/>
							<p class="min-w-0 flex-1 truncate text-[13px] font-medium">{input.label}</p>
						</div>
					{/if}
				</div>

			{/each}
		</div>
	{/if}

	{#if isOutputNode(data.typeId)}
		<div class="border-t border-border/60 px-3 py-2">
			<div class="space-y-1.5">
				<p class="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
					Targets
				</p>
				<OutputTargetPicker
					value={stringValue('segmentIds', '')}
					devices={data.deviceOptions}
					segments={data.segmentOptions}
					onChange={(nextValue) => setProperty('segmentIds', nextValue)}
				/>
			</div>
		</div>
	{/if}

	<!-- Standalone properties (no matching input or output handle) -->
	{#if isCommentNode(data.typeId)}
		<div class="space-y-3 px-3 py-3">
			{#if commentProperty('title')}
				<label class="block space-y-1" title={propertyTooltip(commentProperty('title')!)}>
					<span class="text-[11px] font-semibold uppercase tracking-[0.18em] text-amber-900/70 dark:text-amber-100/70">
						{commentProperty('title')?.label}
					</span>
					<input
						class="nodrag nopan h-8 w-full rounded-md border border-amber-200/80 bg-background/90 px-2 text-[12px] font-medium shadow-sm outline-none transition focus:border-ring focus:ring-4 focus:ring-ring/20 dark:border-amber-500/25 dark:bg-input/30"
						type="text"
						value={stringValue(commentProperty('title')?.key ?? 'title', String(valueFor(commentProperty('title')!) ?? ''))}
						oninput={(event) => setProperty(commentProperty('title')?.key ?? 'title', (event.currentTarget as HTMLInputElement).value)}
					/>
				</label>
			{/if}

			{#if commentProperty('body')}
				<label class="block space-y-1" title={propertyTooltip(commentProperty('body')!)}>
					<span class="text-[11px] font-semibold uppercase tracking-[0.18em] text-amber-900/70 dark:text-amber-100/70">
						{commentProperty('body')?.label}
					</span>
					<textarea
						class="nodrag nopan min-h-28 w-full rounded-lg border border-amber-200/80 bg-background/90 px-2.5 py-2 text-[12px] leading-5 shadow-sm outline-none transition focus:border-ring focus:ring-4 focus:ring-ring/20 dark:border-amber-500/25 dark:bg-input/30"
						placeholder="Describe what this section does, leave a TODO, or explain why the graph is wired this way."
						value={stringValue(commentProperty('body')?.key ?? 'body', String(valueFor(commentProperty('body')!) ?? ''))}
						oninput={(event) => setProperty(commentProperty('body')?.key ?? 'body', (event.currentTarget as HTMLTextAreaElement).value)}
					></textarea>
				</label>
			{/if}
		</div>
	{:else if isConstantColorNode(data.typeId)}
		<div class="border-t border-border/60 px-3 py-2">
			<NodeColorPicker
				hex={colorHex}
				label="Color"
				onchange={(hex) => setColorFromHex(hex)}
			/>
		</div>
	{:else if isGradientNode(data.typeId)}
		<div class="space-y-2 border-t border-border/60 px-3 py-2">
			<GradientEditor
				stops={parseGradientStops(stringValue('stops', '[]'))}
				interpolation={stringValue('interpolation', 'linear')}
				onchange={(next) => setProperty('stops', serializeGradientStops(next))}
			/>
			{#each standaloneProperties.filter((p) => p.key !== 'stops') as property}
				<div class="space-y-1" title={propertyTooltip(property)}>
					{#if property.valueType === 'Float' && hasRange(property)}
						{@render sliderField(property, property.label)}
					{:else}
						<div class="flex h-7 items-center gap-2">
							<span class="shrink-0 text-[12px] font-medium">{property.label}</span>
							{@render inlineEditor(property, property.label)}
						</div>
					{/if}
				</div>
			{/each}
		</div>
	{:else if standaloneProperties.length > 0}
		<div class="space-y-1 border-t border-border/60 px-3 py-2">
			{#each standaloneProperties as property}
				<div class="space-y-1" title={propertyTooltip(property)}>
					{#if property.valueType === 'Float' && hasRange(property)}
						{@render sliderField(property, property.label)}
					{:else if property.valueType === 'Bool'}
						<label class="flex h-7 items-center gap-2 text-[12px] font-medium">
							<input
								class="nodrag nopan size-4 rounded border-border text-primary"
								type="checkbox"
								checked={boolValue(property.key, Boolean(valueFor(property)))}
								onchange={(event) => setProperty(property.key, (event.currentTarget as HTMLInputElement).checked)}
							/>
							{property.label}
						</label>
					{:else}
						<div class="flex h-7 items-center gap-2">
							<span class="shrink-0 text-[12px] font-medium">{property.label}</span>
							{@render inlineEditor(property, property.label)}
						</div>
					{/if}
				</div>
			{/each}
		</div>
	{/if}
</div>
