<script lang="ts">
	import { onMount } from 'svelte';
	import {
		Circle,
		FlipHorizontal,
		FlipVertical,
		Grid3X3,
		Loader2,
		Route,
		RotateCcw,
		RotateCw,
		Save,
		Square,
		Triangle
	} from '@lucide/svelte';
	import { Badge } from '$lib/components/ui/badge';
	import { Button } from '$lib/components/ui/button';
	import {
		Card,
		CardContent,
		CardDescription,
		CardHeader,
		CardTitle
	} from '$lib/components/ui/card';
	import {
		apiGet,
		apiPut,
		toMessage,
		type Device,
		type LedLayoutPoint,
		type Segment
	} from '$lib/lucalights';

	const pointRadius = 0.014;
	const handleSize = 0.026;

	type SelectionBounds = {
		x: number;
		y: number;
		width: number;
		height: number;
		center: LedLayoutPoint;
	};

	type ScaleCorner = 'nw' | 'ne' | 'se' | 'sw';
	const scaleCorners: ScaleCorner[] = ['nw', 'ne', 'se', 'sw'];

	type DragOperation =
		| {
				type: 'move';
				pointerId: number;
				start: LedLayoutPoint;
				indices: number[];
				initialPoints: LedLayoutPoint[];
			}
		| {
				type: 'scale';
				pointerId: number;
				corner: ScaleCorner;
				bounds: SelectionBounds;
				indices: number[];
				initialPoints: LedLayoutPoint[];
			}
		| {
				type: 'rotate';
				pointerId: number;
				center: LedLayoutPoint;
				bounds: SelectionBounds;
				startAngle: number;
				currentDelta: number;
				indices: number[];
				initialPoints: LedLayoutPoint[];
			};

	let devices = $state<Device[]>([]);
	let selectedDeviceId = $state<string | null>(null);
	let selectedSegmentId = $state<string | null>(null);
	let layoutDraft = $state<LedLayoutPoint[]>([]);
	let loading = $state(true);
	let saving = $state(false);
	let dirty = $state(false);
	let errorMessage = $state('');
	let successMessage = $state('');
	let canvasElement = $state<SVGSVGElement | null>(null);
	let selectedLedIndices = $state<number[]>([]);
	let dragOperation = $state<DragOperation | null>(null);
	let marquee = $state<{
		start: LedLayoutPoint;
		current: LedLayoutPoint;
		pointerId: number;
		baseSelection: number[];
	} | null>(null);

	let selectedDevice = $derived(devices.find((device) => device.id === selectedDeviceId) ?? null);
	let selectedSegment = $derived(
		selectedDevice?.segments.find((segment) => segment.id === selectedSegmentId) ?? null
	);
	let totalLedCount = $derived(
		devices.reduce(
			(total, device) =>
				total + device.segments.reduce((segmentTotal, segment) => segmentTotal + segment.length, 0),
			0
		)
	);
	let currentMarqueeRect = $derived(
		marquee
			? {
					x: Math.min(marquee.start.x, marquee.current.x),
					y: Math.min(marquee.start.y, marquee.current.y),
					width: Math.abs(marquee.current.x - marquee.start.x),
					height: Math.abs(marquee.current.y - marquee.start.y)
				}
			: null
	);
	let selectionBounds = $derived(calculateSelectionBounds());
	let activeSelectionBounds = $derived(
		dragOperation?.type === 'rotate' ? dragOperation.bounds : selectionBounds
	);
	let activeSelectionRotation = $derived(
		dragOperation?.type === 'rotate' ? (dragOperation.currentDelta * 180) / Math.PI : 0
	);

	function cloneLayout(segment: Segment | null) {
		return normalizeLayout(segment?.layout ?? [], segment?.length ?? 0);
	}

	function normalizeLayout(layout: LedLayoutPoint[], length: number) {
		const normalized = layout
			.slice(0, length)
			.map((point) => ({ x: clamp01(point.x), y: clamp01(point.y) }));

		while (normalized.length < length) {
			normalized.push(linearPoint(normalized.length, length));
		}

		return normalized;
	}

	function linearPoint(index: number, length: number): LedLayoutPoint {
		return {
			x: length > 1 ? 0.08 + (index / (length - 1)) * 0.84 : 0.5,
			y: 0.5
		};
	}

	function clamp01(value: number) {
		return Number.isFinite(value) ? Math.min(1, Math.max(0, value)) : 0;
	}

	function selectSegment(deviceId: string, segmentId: string) {
		if (dirty && !window.confirm('Discard unsaved layout changes?')) {
			return;
		}

		selectedDeviceId = deviceId;
		selectedSegmentId = segmentId;
		const segment =
			devices.find((device) => device.id === deviceId)?.segments.find((entry) => entry.id === segmentId) ??
			null;
		layoutDraft = cloneLayout(segment);
		selectedLedIndices = [];
		dragOperation = null;
		marquee = null;
		dirty = false;
		errorMessage = '';
		successMessage = '';
	}

	function firstSegment(deviceList: Device[]) {
		for (const device of deviceList) {
			if (device.segments.length > 0) {
				return { device, segment: device.segments[0] };
			}
		}

		return null;
	}

	async function loadDevices(preferredSegmentId: string | null = selectedSegmentId) {
		try {
			const deviceList = await apiGet<Device[]>('/api/devices');
			devices = deviceList;

			const preferred =
				deviceList
					.flatMap((device) => device.segments.map((segment) => ({ device, segment })))
					.find((entry) => entry.segment.id === preferredSegmentId) ?? firstSegment(deviceList);

			if (preferred) {
				selectedDeviceId = preferred.device.id;
				selectedSegmentId = preferred.segment.id;
				layoutDraft = cloneLayout(preferred.segment);
			} else {
				selectedDeviceId = null;
				selectedSegmentId = null;
				layoutDraft = [];
			}

			selectedLedIndices = [];
			dragOperation = null;
			marquee = null;
			dirty = false;
			errorMessage = '';
		} catch (error) {
			errorMessage = toMessage(error);
		} finally {
			loading = false;
		}
	}

	function applyLine() {
		if (!selectedSegment) return;
		layoutDraft = normalizeLayout([], selectedSegment.length);
		selectedLedIndices = [];
		dirty = true;
		successMessage = '';
	}

	function applyGrid() {
		if (!selectedSegment) return;

		const count = selectedSegment.length;
		const columns = Math.max(1, Math.ceil(Math.sqrt(count)));
		const rows = Math.max(1, Math.ceil(count / columns));
		const inset = 0.08;
		const span = 1 - inset * 2;

		layoutDraft = Array.from({ length: count }, (_, index) => {
			const column = index % columns;
			const row = Math.floor(index / columns);
			return {
				x: columns > 1 ? inset + (column / (columns - 1)) * span : 0.5,
				y: rows > 1 ? inset + (row / (rows - 1)) * span : 0.5
			};
		});
		selectedLedIndices = [];
		dirty = true;
		successMessage = '';
	}

	function applySquareOutline() {
		if (!selectedSegment) return;

		const count = selectedSegment.length;
		if (count === 0) {
			layoutDraft = [];
			return;
		}

		const inset = 0.08;
		const edge = 1 - inset * 2;

		layoutDraft = Array.from({ length: count }, (_, index) => {
			const t = count > 1 ? index / count : 0;
			const side = Math.floor(t * 4);
			const local = t * 4 - side;

			if (side === 0) return { x: inset + edge * local, y: inset };
			if (side === 1) return { x: 1 - inset, y: inset + edge * local };
			if (side === 2) return { x: 1 - inset - edge * local, y: 1 - inset };
			return { x: inset, y: 1 - inset - edge * local };
		});
		selectedLedIndices = [];
		dirty = true;
		successMessage = '';
	}

	function applyCircle() {
		if (!selectedSegment) return;

		const count = selectedSegment.length;
		const radius = 0.42;

		layoutDraft = Array.from({ length: count }, (_, index) => {
			const angle = count > 0 ? (index / count) * Math.PI * 2 - Math.PI / 2 : 0;
			return {
				x: 0.5 + Math.cos(angle) * radius,
				y: 0.5 + Math.sin(angle) * radius
			};
		});
		selectedLedIndices = [];
		dirty = true;
		successMessage = '';
	}

	function applyTriangle() {
		if (!selectedSegment) return;

		const count = selectedSegment.length;
		const vertices: LedLayoutPoint[] = [
			{ x: 0.5, y: 0.08 },
			{ x: 0.92, y: 0.86 },
			{ x: 0.08, y: 0.86 }
		];

		layoutDraft = distributeOnPolygon(vertices, count);
		selectedLedIndices = [];
		dirty = true;
		successMessage = '';
	}

	function distributeOnPolygon(vertices: LedLayoutPoint[], count: number) {
		if (count === 0) return [];
		if (count === 1) return [vertices[0]];

		return Array.from({ length: count }, (_, index) => {
			const t = index / count;
			const scaled = t * vertices.length;
			const side = Math.min(vertices.length - 1, Math.floor(scaled));
			const local = scaled - side;
			const start = vertices[side];
			const end = vertices[(side + 1) % vertices.length];

			return {
				x: start.x + (end.x - start.x) * local,
				y: start.y + (end.y - start.y) * local
			};
		});
	}

	function rotateLayout(degrees: number) {
		const radians = (degrees * Math.PI) / 180;
		const cos = Math.cos(radians);
		const sin = Math.sin(radians);
		const rotated = layoutDraft.map((point) => {
			const x = point.x - 0.5;
			const y = point.y - 0.5;
			return {
				x: 0.5 + x * cos - y * sin,
				y: 0.5 + x * sin + y * cos
			};
		});

		layoutDraft = fitIntoCanvas(rotated);
		dirty = true;
		successMessage = '';
	}

	function fitIntoCanvas(points: LedLayoutPoint[]) {
		if (points.length === 0 || points.every((point) => point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1)) {
			return points.map((point) => ({ x: clamp01(point.x), y: clamp01(point.y) }));
		}

		const inset = 0.08;
		const minX = Math.min(...points.map((point) => point.x));
		const maxX = Math.max(...points.map((point) => point.x));
		const minY = Math.min(...points.map((point) => point.y));
		const maxY = Math.max(...points.map((point) => point.y));
		const width = Math.max(0.0001, maxX - minX);
		const height = Math.max(0.0001, maxY - minY);
		const scale = Math.min((1 - inset * 2) / width, (1 - inset * 2) / height);
		const centerX = (minX + maxX) / 2;
		const centerY = (minY + maxY) / 2;

		return points.map((point) => ({
			x: clamp01(0.5 + (point.x - centerX) * scale),
			y: clamp01(0.5 + (point.y - centerY) * scale)
		}));
	}

	function mirrorHorizontal() {
		layoutDraft = layoutDraft.map((point) => ({ x: 1 - point.x, y: point.y }));
		dirty = true;
		successMessage = '';
	}

	function mirrorVertical() {
		layoutDraft = layoutDraft.map((point) => ({ x: point.x, y: 1 - point.y }));
		dirty = true;
		successMessage = '';
	}

	function reverseOrder() {
		layoutDraft = [...layoutDraft].reverse();
		dirty = true;
		successMessage = '';
	}

	function canvasPoint(event: PointerEvent): LedLayoutPoint | null {
		if (!canvasElement) return null;

		const rect = canvasElement.getBoundingClientRect();
		return {
			x: clamp01((event.clientX - rect.left) / rect.width),
			y: clamp01((event.clientY - rect.top) / rect.height)
		};
	}

	function startDrag(index: number, event: PointerEvent) {
		event.stopPropagation();
		const point = canvasPoint(event);
		if (!point) return;

		if (event.shiftKey) {
			toggleSelection(index);
			return;
		}

		const indices = selectedLedIndices.includes(index) ? selectedLedIndices : [index];
		selectedLedIndices = indices;
		dragOperation = {
			type: 'move',
			pointerId: event.pointerId,
			start: point,
			indices,
			initialPoints: layoutDraft.map((entry) => ({ ...entry }))
		};
		(event.currentTarget as Element).setPointerCapture(event.pointerId);
	}

	function dragPoint(event: PointerEvent) {
		if (marquee) {
			updateMarquee(event);
			return;
		}

		if (!dragOperation || event.pointerId !== dragOperation.pointerId) return;

		if (dragOperation.type === 'move') {
			updateMoveDrag(event, dragOperation);
		} else if (dragOperation.type === 'scale') {
			updateScaleDrag(event, dragOperation);
		} else {
			updateRotateDrag(event, dragOperation);
		}
	}

	function endDrag(event: PointerEvent) {
		if (marquee) {
			finishMarquee(event);
			return;
		}

		if (dragOperation && event.pointerId === dragOperation.pointerId) {
			const target = event.currentTarget as Element;
			if (target.hasPointerCapture(event.pointerId)) {
				target.releasePointerCapture(event.pointerId);
			}
			dragOperation = null;
		}
	}

	function updateMoveDrag(event: PointerEvent, operation: Extract<DragOperation, { type: 'move' }>) {
		const point = canvasPoint(event);
		if (!point) return;

		const dx = point.x - operation.start.x;
		const dy = point.y - operation.start.y;
		const selected = new Set(operation.indices);

		layoutDraft = operation.initialPoints.map((entry, index) =>
			selected.has(index)
				? {
						x: clamp01(entry.x + dx),
						y: clamp01(entry.y + dy)
					}
				: entry
		);
		dirty = true;
		successMessage = '';
	}

	function updateScaleDrag(event: PointerEvent, operation: Extract<DragOperation, { type: 'scale' }>) {
		const point = canvasPoint(event);
		if (!point) return;

		const anchor = event.ctrlKey
			? oppositeCornerPoint(operation.bounds, operation.corner)
			: operation.bounds.center;
		const startCorner = cornerPoint(operation.bounds, operation.corner);
		const startDx = startCorner.x - anchor.x;
		const startDy = startCorner.y - anchor.y;
		let scaleX = Math.abs(startDx) < 0.0001 ? 1 : (point.x - anchor.x) / startDx;
		let scaleY = Math.abs(startDy) < 0.0001 ? 1 : (point.y - anchor.y) / startDy;

		if (event.shiftKey) {
			const uniformScale = Math.max(Math.abs(scaleX), Math.abs(scaleY));
			scaleX = Math.sign(scaleX || 1) * uniformScale;
			scaleY = Math.sign(scaleY || 1) * uniformScale;
		}

		const selected = new Set(operation.indices);

		layoutDraft = operation.initialPoints.map((entry, index) =>
			selected.has(index)
				? {
						x: clamp01(anchor.x + (entry.x - anchor.x) * scaleX),
						y: clamp01(anchor.y + (entry.y - anchor.y) * scaleY)
					}
				: entry
		);
		dirty = true;
		successMessage = '';
	}

	function updateRotateDrag(event: PointerEvent, operation: Extract<DragOperation, { type: 'rotate' }>) {
		const point = canvasPoint(event);
		if (!point) return;

		const currentAngle = Math.atan2(point.y - operation.center.y, point.x - operation.center.x);
		const rawDelta = currentAngle - operation.startAngle;
		const snapStep = Math.PI / 12;
		const delta = event.shiftKey
			? Math.round(rawDelta / snapStep) * snapStep
			: rawDelta;
		const cos = Math.cos(delta);
		const sin = Math.sin(delta);
		const selected = new Set(operation.indices);

		dragOperation = {
			...operation,
			currentDelta: delta
		};

		layoutDraft = operation.initialPoints.map((entry, index) => {
			if (!selected.has(index)) return entry;

			const x = entry.x - operation.center.x;
			const y = entry.y - operation.center.y;
			return {
				x: clamp01(operation.center.x + x * cos - y * sin),
				y: clamp01(operation.center.y + x * sin + y * cos)
			};
		});
		dirty = true;
		successMessage = '';
	}

	function startScale(corner: ScaleCorner, event: PointerEvent) {
		event.stopPropagation();
		if (!selectionBounds || selectedLedIndices.length === 0) return;

		dragOperation = {
			type: 'scale',
			pointerId: event.pointerId,
			corner,
			bounds: selectionBounds,
			indices: selectedLedIndices,
			initialPoints: layoutDraft.map((entry) => ({ ...entry }))
		};
		(event.currentTarget as Element).setPointerCapture(event.pointerId);
	}

	function startSelectionMove(event: PointerEvent) {
		event.stopPropagation();
		const point = canvasPoint(event);
		if (!point || selectedLedIndices.length === 0) return;

		dragOperation = {
			type: 'move',
			pointerId: event.pointerId,
			start: point,
			indices: selectedLedIndices,
			initialPoints: layoutDraft.map((entry) => ({ ...entry }))
		};
		(event.currentTarget as Element).setPointerCapture(event.pointerId);
	}

	function startRotate(event: PointerEvent) {
		event.stopPropagation();
		const point = canvasPoint(event);
		if (!point || !selectionBounds || selectedLedIndices.length === 0) return;

		dragOperation = {
			type: 'rotate',
			pointerId: event.pointerId,
			center: selectionBounds.center,
			bounds: selectionBounds,
			startAngle: Math.atan2(point.y - selectionBounds.center.y, point.x - selectionBounds.center.x),
			currentDelta: 0,
			indices: selectedLedIndices,
			initialPoints: layoutDraft.map((entry) => ({ ...entry }))
		};
		(event.currentTarget as Element).setPointerCapture(event.pointerId);
	}

	function startMarquee(event: PointerEvent) {
		const point = canvasPoint(event);
		if (!point) return;

		(event.currentTarget as Element).setPointerCapture(event.pointerId);
		marquee = {
			start: point,
			current: point,
			pointerId: event.pointerId,
			baseSelection: event.shiftKey ? selectedLedIndices : []
		};
		if (!event.shiftKey) {
			selectedLedIndices = [];
		}
	}

	function updateMarquee(event: PointerEvent) {
		if (!marquee || event.pointerId !== marquee.pointerId) return;

		const point = canvasPoint(event);
		if (!point) return;

		marquee = {
			...marquee,
			current: point
		};
		selectedLedIndices = mergeSelections(marquee.baseSelection, indicesInMarquee(marquee.start, point));
	}

	function finishMarquee(event: PointerEvent) {
		if (!marquee || event.pointerId !== marquee.pointerId) return;

		(event.currentTarget as Element).releasePointerCapture(event.pointerId);
		const point = canvasPoint(event);
		const selected = point ? indicesInMarquee(marquee.start, point) : [];
		const moved =
			point &&
			(Math.abs(point.x - marquee.start.x) > 0.004 || Math.abs(point.y - marquee.start.y) > 0.004);

		selectedLedIndices = moved ? mergeSelections(marquee.baseSelection, selected) : marquee.baseSelection;
		marquee = null;
	}

	function indicesInMarquee(start: LedLayoutPoint, end: LedLayoutPoint) {
		const minX = Math.min(start.x, end.x);
		const maxX = Math.max(start.x, end.x);
		const minY = Math.min(start.y, end.y);
		const maxY = Math.max(start.y, end.y);

		return layoutDraft
			.map((point, index) => ({ point, index }))
			.filter(({ point }) => point.x >= minX && point.x <= maxX && point.y >= minY && point.y <= maxY)
			.map(({ index }) => index);
	}

	function isSelected(index: number) {
		return selectedLedIndices.includes(index);
	}

	function toggleSelection(index: number) {
		selectedLedIndices = selectedLedIndices.includes(index)
			? selectedLedIndices.filter((entry) => entry !== index)
			: [...selectedLedIndices, index].sort((a, b) => a - b);
	}

	function mergeSelections(a: number[], b: number[]) {
		return Array.from(new Set([...a, ...b])).sort((left, right) => left - right);
	}

	function calculateSelectionBounds(): SelectionBounds | null {
		if (selectedLedIndices.length === 0) return null;

		const points = selectedLedIndices
			.map((index) => layoutDraft[index])
			.filter((point): point is LedLayoutPoint => !!point);

		if (points.length === 0) return null;

		const minX = Math.min(...points.map((point) => point.x));
		const maxX = Math.max(...points.map((point) => point.x));
		const minY = Math.min(...points.map((point) => point.y));
		const maxY = Math.max(...points.map((point) => point.y));
		const padding = points.length === 1 ? 0.04 : 0.02;
		const x = clamp01(minX - padding);
		const y = clamp01(minY - padding);
		const width = Math.min(1 - x, maxX - minX + padding * 2);
		const height = Math.min(1 - y, maxY - minY + padding * 2);

		return {
			x,
			y,
			width,
			height,
			center: {
				x: x + width / 2,
				y: y + height / 2
			}
		};
	}

	function cornerPoint(bounds: SelectionBounds, corner: ScaleCorner): LedLayoutPoint {
		switch (corner) {
			case 'nw':
				return { x: bounds.x, y: bounds.y };
			case 'ne':
				return { x: bounds.x + bounds.width, y: bounds.y };
			case 'se':
				return { x: bounds.x + bounds.width, y: bounds.y + bounds.height };
			case 'sw':
				return { x: bounds.x, y: bounds.y + bounds.height };
		}
	}

	function oppositeCornerPoint(bounds: SelectionBounds, corner: ScaleCorner): LedLayoutPoint {
		switch (corner) {
			case 'nw':
				return cornerPoint(bounds, 'se');
			case 'ne':
				return cornerPoint(bounds, 'sw');
			case 'se':
				return cornerPoint(bounds, 'nw');
			case 'sw':
				return cornerPoint(bounds, 'ne');
		}
	}

	function scaleHandleCursor(corner: ScaleCorner) {
		return corner === 'nw' || corner === 'se' ? 'cursor-nwse-resize' : 'cursor-nesw-resize';
	}

	async function saveLayout() {
		if (!selectedDevice || !selectedSegment) return;

		saving = true;

		try {
			const updatedSegment = await apiPut<Segment>(
				`/api/devices/${encodeURIComponent(selectedDevice.id)}/segments/${encodeURIComponent(selectedSegment.id)}`,
				{
					...selectedSegment,
					layout: normalizeLayout(layoutDraft, selectedSegment.length)
				}
			);

			devices = devices.map((device) =>
				device.id === selectedDevice.id
					? {
							...device,
							segments: device.segments.map((segment) =>
								segment.id === updatedSegment.id ? updatedSegment : segment
							)
						}
					: device
			);
			layoutDraft = cloneLayout(updatedSegment);
			dirty = false;
			errorMessage = '';
			successMessage = `Saved ${updatedSegment.name} layout.`;
		} catch (error) {
			errorMessage = toMessage(error);
		} finally {
			saving = false;
		}
	}

	function resetDraft() {
		layoutDraft = cloneLayout(selectedSegment);
		selectedLedIndices = [];
		dragOperation = null;
		marquee = null;
		dirty = false;
		errorMessage = '';
		successMessage = '';
	}

	function startSegmentLayoutDrag(segment: Segment, event: DragEvent) {
		if (!event.dataTransfer) return;

		event.dataTransfer.effectAllowed = 'copy';
		event.dataTransfer.setData(
			'application/x-lucalights-segment-layout',
			JSON.stringify({
				name: segment.name,
				length: segment.length,
				layout: normalizeLayout(segment.layout ?? [], segment.length)
			})
		);
		event.dataTransfer.setData('text/plain', `${segment.name} layout`);
	}

	function allowLayoutDrop(event: DragEvent) {
		if (event.dataTransfer?.types.includes('application/x-lucalights-segment-layout')) {
			event.preventDefault();
			event.dataTransfer.dropEffect = 'copy';
		}
	}

	function dropSegmentLayout(event: DragEvent) {
		if (!selectedSegment || !event.dataTransfer) return;

		const rawLayout = event.dataTransfer.getData('application/x-lucalights-segment-layout');
		if (!rawLayout) return;

		event.preventDefault();

		try {
			const payload = JSON.parse(rawLayout) as {
				name?: string;
				length?: number;
				layout?: LedLayoutPoint[];
			};
			const sourceName = payload.name ?? 'segment';

			if (
				!window.confirm(
					`Replace ${selectedSegment.name}'s current layout with ${sourceName}'s layout?`
				)
			) {
				return;
			}

			const sourceLayout = normalizeLayout(payload.layout ?? [], Math.max(0, Number(payload.length) || 0));

			layoutDraft = resampleLayout(sourceLayout, selectedSegment.length);
			selectedLedIndices = [];
			dragOperation = null;
			marquee = null;
			dirty = true;
			errorMessage = '';
			successMessage = `Copied ${sourceName} layout.`;
		} catch {
			errorMessage = 'Could not read the dropped segment layout.';
		}
	}

	function resampleLayout(sourceLayout: LedLayoutPoint[], targetLength: number) {
		if (targetLength <= 0) return [];
		if (sourceLayout.length === 0) return normalizeLayout([], targetLength);
		if (sourceLayout.length === 1) {
			return Array.from({ length: targetLength }, () => ({ ...sourceLayout[0] }));
		}

		return Array.from({ length: targetLength }, (_, index) => {
			const sourcePosition = targetLength > 1
				? (index / (targetLength - 1)) * (sourceLayout.length - 1)
				: 0;
			const lowerIndex = Math.floor(sourcePosition);
			const upperIndex = Math.min(sourceLayout.length - 1, lowerIndex + 1);
			const t = sourcePosition - lowerIndex;
			const a = sourceLayout[lowerIndex];
			const b = sourceLayout[upperIndex];

			return {
				x: a.x + (b.x - a.x) * t,
				y: a.y + (b.y - a.y) * t
			};
		});
	}

	onMount(() => {
		void loadDevices();
	});
</script>

<svelte:head>
	<title>Layout Editor - LucaLights</title>
	<meta name="description" content="Author normalized LED positions for LucaLights segments." />
</svelte:head>

<div class="relative min-h-screen overflow-hidden bg-(image:--page-gradient) text-foreground">
	<div class="pointer-events-none absolute inset-0 bg-(image:--page-overlay)"></div>

	<section class="relative mx-auto flex max-w-7xl flex-col gap-6 px-4 py-6 sm:px-6 lg:px-8 lg:py-10">
		<div class="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
			<div class="max-w-3xl space-y-2">
				<h1 class="text-3xl font-semibold tracking-tight sm:text-4xl">Layout Editor</h1>
				<p class="max-w-2xl text-sm leading-6 text-muted-foreground sm:text-base">
					Place LEDs in normalized space so Pixel Info can feed real 2D positions into graph effects.
				</p>
			</div>

			<div class="flex flex-wrap items-center gap-2">
				<Button variant="outline" onclick={resetDraft} disabled={!selectedSegment || !dirty || saving}>
					<RotateCcw />
					Reset
				</Button>
				<Button onclick={saveLayout} disabled={!selectedSegment || !dirty || saving}>
					{#if saving}
						<Loader2 class="animate-spin" />
					{:else}
						<Save />
					{/if}
					Save Layout
				</Button>
			</div>
		</div>

		{#if errorMessage}
			<div class="rounded-2xl border border-destructive/30 bg-destructive/10 px-4 py-3 text-sm text-destructive shadow-sm">
				{errorMessage}
			</div>
		{/if}

		{#if successMessage}
			<div class="rounded-2xl border border-border/80 bg-surface-card px-4 py-3 text-sm text-foreground shadow-sm backdrop-blur">
				{successMessage}
			</div>
		{/if}

		<div class="grid min-h-[42rem] gap-6 xl:grid-cols-[18rem_minmax(0,1fr)_18rem]">
			<Card class="max-h-[42rem] border-surface-card-border bg-surface-card-alt shadow-sm backdrop-blur">
				<CardHeader>
					<CardTitle>Segments</CardTitle>
					<CardDescription>{devices.length} devices · {totalLedCount} LEDs</CardDescription>
				</CardHeader>
				<CardContent class="max-h-[34rem] space-y-3 overflow-y-auto pr-2">
					{#if loading}
						<div class="flex items-center gap-2 rounded-2xl border border-border/70 bg-background/65 px-4 py-3 text-sm text-muted-foreground">
							<Loader2 class="size-4 animate-spin" />
							Loading layouts...
						</div>
					{:else if devices.some((device) => device.segments.length > 0)}
						{#each devices as device}
							{#if device.segments.length > 0}
								<div class="space-y-2">
									<p class="px-1 text-xs font-medium uppercase tracking-[0.16em] text-muted-foreground">
										{device.name}
									</p>
									{#each device.segments as segment}
										<button
											type="button"
											draggable="true"
											class={`w-full rounded-xl border p-3 text-left transition ${
												segment.id === selectedSegmentId
													? 'border-primary/35 bg-primary/8 shadow-sm'
													: 'border-border/70 bg-background/65 hover:border-border hover:bg-background/80'
											}`}
											onclick={() => selectSegment(device.id, segment.id)}
											ondragstart={(event) => startSegmentLayoutDrag(segment, event)}
										>
											<div class="flex items-center justify-between gap-2">
												<span class="text-sm font-semibold">{segment.name}</span>
												<Badge variant="outline">{segment.length}</Badge>
											</div>
											<p class="mt-2 text-xs text-muted-foreground">
												{segment.layout?.length ?? 0} saved positions
											</p>
										</button>
									{/each}
								</div>
							{/if}
						{/each}
					{:else}
						<div class="rounded-2xl border border-dashed border-border/80 bg-background/50 px-4 py-8 text-center text-sm text-muted-foreground">
							Add a device segment before authoring a layout.
						</div>
					{/if}
				</CardContent>
			</Card>

			<Card class="min-h-0 border-surface-card-border bg-surface-card-alt shadow-sm backdrop-blur">
				<CardHeader class="space-y-3">
					<div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
						<div class="space-y-1">
							<CardTitle>{selectedSegment?.name ?? 'No segment selected'}</CardTitle>
							<CardDescription>
								{selectedDevice?.name ?? 'Choose a segment to edit its normalized LED positions.'}
							</CardDescription>
						</div>
						<div class="flex flex-wrap gap-2">
							<Badge variant="outline">{layoutDraft.length} points</Badge>
							{#if selectedLedIndices.length > 0}
								<Badge variant="outline">{selectedLedIndices.length} selected</Badge>
							{/if}
							{#if dirty}
								<Badge variant="secondary">Unsaved</Badge>
							{/if}
						</div>
					</div>
				</CardHeader>
				<CardContent>
					<div class="overflow-hidden rounded-2xl border border-border/70 bg-background/70 p-4">
						<svg
							bind:this={canvasElement}
							viewBox="0 0 1 1"
							class="aspect-square w-full touch-none rounded-xl bg-[linear-gradient(to_right,rgba(127,127,127,0.14)_1px,transparent_1px),linear-gradient(to_bottom,rgba(127,127,127,0.14)_1px,transparent_1px)] bg-[size:10%_10%]"
							role="img"
							aria-label="LED layout canvas"
							onpointermove={dragPoint}
							onpointerup={endDrag}
							onpointercancel={endDrag}
							onpointerdown={startMarquee}
							ondragover={allowLayoutDrop}
							ondrop={dropSegmentLayout}
						>
							<rect x="0" y="0" width="1" height="1" fill="transparent" />
							{#if layoutDraft.length > 1}
								<polyline
									points={layoutDraft.map((point) => `${point.x},${point.y}`).join(' ')}
									fill="none"
									stroke="currentColor"
									stroke-opacity="0.24"
									stroke-width="0.006"
								/>
							{/if}
							{#if currentMarqueeRect}
								<rect
									x={currentMarqueeRect.x}
									y={currentMarqueeRect.y}
									width={currentMarqueeRect.width}
									height={currentMarqueeRect.height}
									class="fill-primary/15 stroke-primary"
									stroke-width="0.004"
									stroke-dasharray="0.018 0.012"
								/>
							{/if}
							{#each layoutDraft as point, index}
								<g
									role="button"
									tabindex="0"
									aria-label={`Move LED ${index + 1}`}
									class="cursor-grab active:cursor-grabbing"
									onpointerdown={(event) => startDrag(index, event)}
								>
									<circle
										cx={point.x}
										cy={point.y}
										r={pointRadius}
										class={index === 0
											? 'fill-emerald-400 stroke-background'
											: isSelected(index)
												? 'fill-amber-300 stroke-primary'
												: 'fill-primary stroke-background'}
										stroke-width={isSelected(index) ? '0.01' : '0.006'}
									/>
								</g>
							{/each}
							{#if activeSelectionBounds}
								<g
									transform={`rotate(${activeSelectionRotation} ${activeSelectionBounds.center.x} ${activeSelectionBounds.center.y})`}
								>
									<rect
										role="button"
										tabindex="0"
										aria-label="Move selected LEDs"
										x={activeSelectionBounds.x}
										y={activeSelectionBounds.y}
										width={activeSelectionBounds.width}
										height={activeSelectionBounds.height}
										class="cursor-move fill-transparent stroke-amber-300"
										stroke-width="0.004"
										stroke-dasharray="0.016 0.01"
										onpointerdown={startSelectionMove}
									/>
									<line
										x1={activeSelectionBounds.center.x}
										y1={activeSelectionBounds.y}
										x2={activeSelectionBounds.center.x}
										y2={Math.max(0, activeSelectionBounds.y - 0.055)}
										class="stroke-amber-300"
										stroke-width="0.004"
									/>
									<circle
										role="button"
										tabindex="0"
										aria-label="Rotate selected LEDs"
										cx={activeSelectionBounds.center.x}
										cy={Math.max(0.018, activeSelectionBounds.y - 0.07)}
										r="0.018"
										class="cursor-grab fill-background stroke-amber-300 active:cursor-grabbing"
										stroke-width="0.006"
										onpointerdown={startRotate}
									/>
									{#each scaleCorners as corner}
										{@const handle = cornerPoint(activeSelectionBounds, corner)}
										<rect
											role="button"
											tabindex="0"
											aria-label={`Scale selected LEDs from ${corner}`}
											x={handle.x - handleSize / 2}
											y={handle.y - handleSize / 2}
											width={handleSize}
											height={handleSize}
											rx="0.004"
											class={`${scaleHandleCursor(corner)} fill-background stroke-amber-300`}
											stroke-width="0.006"
											onpointerdown={(event) => startScale(corner, event)}
										/>
									{/each}
								</g>
							{/if}
						</svg>
					</div>
				</CardContent>
			</Card>

			<Card class="border-surface-card-border bg-surface-card-alt shadow-sm backdrop-blur">
				<CardHeader>
					<CardTitle>Tools</CardTitle>
					<CardDescription>Generate a starting shape, then drag points into place.</CardDescription>
				</CardHeader>
				<CardContent class="space-y-5">
					<div class="grid gap-2">
						<Button variant="outline" onclick={applyLine} disabled={!selectedSegment}>
							<Route />
							Line
						</Button>
						<Button variant="outline" onclick={applyGrid} disabled={!selectedSegment}>
							<Grid3X3 />
							Grid
						</Button>
						<Button variant="outline" onclick={applySquareOutline} disabled={!selectedSegment}>
							<Square />
							Square Outline
						</Button>
						<Button variant="outline" onclick={applyCircle} disabled={!selectedSegment}>
							<Circle />
							Circle
						</Button>
						<Button variant="outline" onclick={applyTriangle} disabled={!selectedSegment}>
							<Triangle />
							Triangle
						</Button>
					</div>

					<div class="space-y-3">
						<p class="text-sm font-medium">Transform</p>
						<div class="grid grid-cols-2 gap-2">
							<Button variant="outline" onclick={() => rotateLayout(90)} disabled={layoutDraft.length === 0}>
								<RotateCw />
								90°
							</Button>
							<Button variant="outline" onclick={() => rotateLayout(-90)} disabled={layoutDraft.length === 0}>
								<RotateCcw />
								90°
							</Button>
							<Button variant="outline" onclick={mirrorHorizontal} disabled={layoutDraft.length === 0}>
								<FlipHorizontal />
								Mirror X
							</Button>
							<Button variant="outline" onclick={mirrorVertical} disabled={layoutDraft.length === 0}>
								<FlipVertical />
								Mirror Y
							</Button>
						</div>

						<div class="grid gap-2">
							<Button variant="outline" onclick={reverseOrder} disabled={layoutDraft.length === 0}>
								<Route />
								Reverse LED Order
							</Button>
						</div>
					</div>

					<p class="text-sm leading-6 text-muted-foreground">
						Drag a segment from the left onto the canvas to copy and resample its layout. Pixel Info
						can read coordinates as Layout X and Layout Y.
					</p>
				</CardContent>
			</Card>
		</div>
	</section>
</div>
