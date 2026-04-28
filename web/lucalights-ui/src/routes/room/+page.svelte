<script lang="ts">
	import { onMount } from 'svelte';
	import {
		BoxSelect,
		Grid3X3,
		Loader2,
		Plus,
		RotateCcw,
		Save,
		Trash2
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
		type RoomLayout,
		type Segment,
		type SegmentPlacement
	} from '$lib/lucalights';

	const pointRadius = 0.0065;
	const handleSize = 0.026;

	type ScaleCorner = 'nw' | 'ne' | 'se' | 'sw';
	const scaleCorners: ScaleCorner[] = ['nw', 'ne', 'se', 'sw'];

	type SegmentEntry = {
		device: Device;
		segment: Segment;
	};

	type PlacementBounds = {
		x: number;
		y: number;
		width: number;
		height: number;
		center: LedLayoutPoint;
	};

	type DragOperation =
		| {
				type: 'move';
				pointerId: number;
				segmentIds: string[];
				start: LedLayoutPoint;
				initialPlacements: SegmentPlacement[];
			}
		| {
				type: 'rotate';
				pointerId: number;
				segmentIds: string[];
				bounds: PlacementBounds;
				startAngle: number;
				currentDelta: number;
				initialPlacements: SegmentPlacement[];
			}
		| {
				type: 'scale';
				pointerId: number;
				segmentIds: string[];
				corner: ScaleCorner;
				bounds: PlacementBounds;
				initialPlacements: SegmentPlacement[];
			};

	let devices = $state<Device[]>([]);
	let roomLayout = $state<RoomLayout>({ placements: [] });
	let selectedSegmentIds = $state<string[]>([]);
	let loading = $state(true);
	let saving = $state(false);
	let dirty = $state(false);
	let errorMessage = $state('');
	let successMessage = $state('');
	let canvasElement = $state<SVGSVGElement | null>(null);
	let dragOperation = $state<DragOperation | null>(null);
	let marquee = $state<{
		start: LedLayoutPoint;
		current: LedLayoutPoint;
		pointerId: number;
		baseSelection: string[];
	} | null>(null);

	let segmentEntries = $derived(
		devices.flatMap((device) => device.segments.map((segment) => ({ device, segment })))
	);
	let placedSegmentIds = $derived(new Set(roomLayout.placements.map((placement) => placement.segmentId)));
	let selectedSegmentId = $derived(selectedSegmentIds[0] ?? null);
	let selectedPlacements = $derived(
		selectedSegmentIds
			.map((segmentId) => roomLayout.placements.find((placement) => placement.segmentId === segmentId))
			.filter((placement): placement is SegmentPlacement => !!placement)
	);
	let selectedPlacement = $derived(
		roomLayout.placements.find((placement) => placement.segmentId === selectedSegmentId) ?? null
	);
	let selectedSegment = $derived(findSegment(selectedSegmentId));
	let selectedBounds = $derived(
		selectedPlacements.length > 0 ? calculateGroupBounds(selectedPlacements) : null
	);
	let activeSelectionBounds = $derived(
		dragOperation?.type === 'rotate' ? dragOperation.bounds : selectedBounds
	);
	let activeSelectionRotation = $derived(
		dragOperation?.type === 'rotate' ? dragOperation.currentDelta : 0
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

	function normalizePlacement(placement: SegmentPlacement): SegmentPlacement {
		return {
			segmentId: placement.segmentId,
			x: finite(placement.x, 0.5),
			y: finite(placement.y, 0.5),
			rotation: finite(placement.rotation, 0),
			scaleX: Math.max(0.001, Math.abs(finite(placement.scaleX, 0.25))),
			scaleY: Math.max(0.001, Math.abs(finite(placement.scaleY, 0.25)))
		};
	}

	function finite(value: number, fallback: number) {
		return Number.isFinite(value) ? value : fallback;
	}

	function clamp01(value: number) {
		return Number.isFinite(value) ? Math.min(1, Math.max(0, value)) : 0;
	}

	function findSegment(segmentId: string | null) {
		if (!segmentId) return null;
		for (const device of devices) {
			const segment = device.segments.find((entry) => entry.id === segmentId);
			if (segment) return segment;
		}
		return null;
	}

	function findSegmentEntry(segmentId: string) {
		return segmentEntries.find((entry) => entry.segment.id === segmentId) ?? null;
	}

	function pointForSegment(segment: Segment, index: number): LedLayoutPoint {
		const point = segment.layout?.[index];
		if (point) return { x: clamp01(point.x), y: clamp01(point.y) };
		return {
			x: segment.length > 1 ? index / (segment.length - 1) : 0.5,
			y: 0.5
		};
	}

	function transformPoint(point: LedLayoutPoint, placement: SegmentPlacement): LedLayoutPoint {
		const radians = (placement.rotation * Math.PI) / 180;
		const cos = Math.cos(radians);
		const sin = Math.sin(radians);
		const x = (point.x - 0.5) * placement.scaleX;
		const y = (point.y - 0.5) * placement.scaleY;

		return {
			x: placement.x + x * cos - y * sin,
			y: placement.y + x * sin + y * cos
		};
	}

	function localUnitCorner(placement: SegmentPlacement, corner: ScaleCorner): LedLayoutPoint {
		const local = {
			x: corner === 'ne' || corner === 'se' ? 0.5 : -0.5,
			y: corner === 'sw' || corner === 'se' ? 0.5 : -0.5
		};
		const radians = (placement.rotation * Math.PI) / 180;
		const cos = Math.cos(radians);
		const sin = Math.sin(radians);
		const x = local.x * placement.scaleX;
		const y = local.y * placement.scaleY;

		return {
			x: placement.x + x * cos - y * sin,
			y: placement.y + x * sin + y * cos
		};
	}

	function inverseRotatedPoint(point: LedLayoutPoint, placement: SegmentPlacement): LedLayoutPoint {
		const radians = (-placement.rotation * Math.PI) / 180;
		const cos = Math.cos(radians);
		const sin = Math.sin(radians);
		const x = point.x - placement.x;
		const y = point.y - placement.y;

		return {
			x: x * cos - y * sin,
			y: x * sin + y * cos
		};
	}

	function transformedPoints(segment: Segment, placement: SegmentPlacement) {
		return Array.from({ length: segment.length }, (_, index) =>
			transformPoint(pointForSegment(segment, index), placement)
		);
	}

	function calculatePlacementBounds(segment: Segment, placement: SegmentPlacement): PlacementBounds | null {
		const points = transformedPoints(segment, placement);
		if (points.length === 0) {
			points.push({ x: placement.x, y: placement.y });
		}

		const minX = Math.min(...points.map((point) => point.x));
		const maxX = Math.max(...points.map((point) => point.x));
		const minY = Math.min(...points.map((point) => point.y));
		const maxY = Math.max(...points.map((point) => point.y));
		const padding = 0.028;
		const rawWidth = maxX - minX + padding * 2;
		const rawHeight = maxY - minY + padding * 2;
		const width = Math.max(0.08, rawWidth);
		const height = Math.max(0.08, rawHeight);
		const center = {
			x: (minX + maxX) / 2,
			y: (minY + maxY) / 2
		};
		const x = center.x - width / 2;
		const y = center.y - height / 2;

		return {
			x,
			y,
			width,
			height,
			center
		};
	}

	function calculateGroupBounds(placements: SegmentPlacement[]): PlacementBounds | null {
		const bounds = placements
			.map((placement) => {
				const segment = findSegment(placement.segmentId);
				return segment ? calculatePlacementBounds(segment, placement) : null;
			})
			.filter((entry): entry is PlacementBounds => !!entry);

		if (bounds.length === 0) return null;

		const minX = Math.min(...bounds.map((entry) => entry.x));
		const maxX = Math.max(...bounds.map((entry) => entry.x + entry.width));
		const minY = Math.min(...bounds.map((entry) => entry.y));
		const maxY = Math.max(...bounds.map((entry) => entry.y + entry.height));

		return {
			x: minX,
			y: minY,
			width: maxX - minX,
			height: maxY - minY,
			center: {
				x: (minX + maxX) / 2,
				y: (minY + maxY) / 2
			}
		};
	}

	function selectionContains(segmentId: string) {
		return selectedSegmentIds.includes(segmentId);
	}

	function setSelection(segmentIds: string[]) {
		selectedSegmentIds = Array.from(new Set(segmentIds)).filter((segmentId) =>
			roomLayout.placements.some((placement) => placement.segmentId === segmentId)
		);
	}

	function toggleSelection(segmentId: string) {
		setSelection(
			selectedSegmentIds.includes(segmentId)
				? selectedSegmentIds.filter((entry) => entry !== segmentId)
				: [...selectedSegmentIds, segmentId]
		);
	}

	function mergeSelections(a: string[], b: string[]) {
		return Array.from(new Set([...a, ...b]));
	}

	function placementsInMarquee(start: LedLayoutPoint, end: LedLayoutPoint) {
		const minX = Math.min(start.x, end.x);
		const maxX = Math.max(start.x, end.x);
		const minY = Math.min(start.y, end.y);
		const maxY = Math.max(start.y, end.y);

		return roomLayout.placements
			.filter((placement) => {
				const segment = findSegment(placement.segmentId);
				if (!segment) return false;
				const bounds = calculatePlacementBounds(segment, placement);
				if (!bounds) return false;

				return (
					bounds.x <= maxX &&
					bounds.x + bounds.width >= minX &&
					bounds.y <= maxY &&
					bounds.y + bounds.height >= minY
				);
			})
			.map((placement) => placement.segmentId);
	}

	function canvasPoint(event: PointerEvent): LedLayoutPoint | null {
		if (!canvasElement) return null;

		const rect = canvasElement.getBoundingClientRect();
		return {
			x: (event.clientX - rect.left) / rect.width,
			y: (event.clientY - rect.top) / rect.height
		};
	}

	async function loadRoom() {
		loading = true;

		try {
			const [deviceList, layout] = await Promise.all([
				apiGet<Device[]>('/api/devices'),
				apiGet<RoomLayout>('/api/room-layout')
			]);
			devices = deviceList;
			roomLayout = {
				placements: (layout.placements ?? []).map(normalizePlacement)
			};
			setSelection(roomLayout.placements[0] ? [roomLayout.placements[0].segmentId] : []);
			dirty = false;
			errorMessage = '';
		} catch (error) {
			errorMessage = toMessage(error);
		} finally {
			loading = false;
		}
	}

	async function saveRoom() {
		saving = true;

		try {
			const saved = await apiPut<RoomLayout>('/api/room-layout', {
				placements: roomLayout.placements.map(normalizePlacement)
			});
			roomLayout = { placements: (saved.placements ?? []).map(normalizePlacement) };
			dirty = false;
			errorMessage = '';
			successMessage = 'Saved room layout.';
		} catch (error) {
			errorMessage = toMessage(error);
		} finally {
			saving = false;
		}
	}

	function addPlacement(entry: SegmentEntry) {
		if (placedSegmentIds.has(entry.segment.id)) {
			setSelection([entry.segment.id]);
			return;
		}

		const index = roomLayout.placements.length;
		const columns = 4;
		const placement = normalizePlacement({
			segmentId: entry.segment.id,
			x: 0.2 + (index % columns) * 0.18,
			y: 0.24 + Math.floor(index / columns) * 0.18,
			rotation: 0,
			scaleX: 0.18,
			scaleY: 0.18
		});

		roomLayout = { placements: [...roomLayout.placements, placement] };
		setSelection([entry.segment.id]);
		dirty = true;
		successMessage = '';
	}

	function removeSelectedPlacement() {
		if (selectedSegmentIds.length === 0) return;
		const removed = new Set(selectedSegmentIds);

		roomLayout = {
			placements: roomLayout.placements.filter((placement) => !removed.has(placement.segmentId))
		};
		setSelection(roomLayout.placements[0] ? [roomLayout.placements[0].segmentId] : []);
		dirty = true;
		successMessage = '';
	}

	function resetRoom() {
		void loadRoom();
	}

	function updatePlacement(segmentId: string, updater: (placement: SegmentPlacement) => SegmentPlacement) {
		updatePlacements([segmentId], (placement) => updater(placement));
	}

	function updatePlacements(
		segmentIds: string[],
		updater: (placement: SegmentPlacement) => SegmentPlacement
	) {
		const selected = new Set(segmentIds);
		roomLayout = {
			placements: roomLayout.placements.map((placement) =>
				selected.has(placement.segmentId) ? normalizePlacement(updater(placement)) : placement
			)
		};
		dirty = true;
		successMessage = '';
	}

	function selectPlacement(segmentId: string, event?: PointerEvent) {
		event?.stopPropagation();
		if (event?.shiftKey) {
			toggleSelection(segmentId);
		} else {
			setSelection([segmentId]);
		}
	}

	function startMove(segmentId: string, event: PointerEvent) {
		event.stopPropagation();
		const point = canvasPoint(event);
		const placement = roomLayout.placements.find((entry) => entry.segmentId === segmentId);
		if (!point || !placement) return;

		if (event.shiftKey) {
			toggleSelection(segmentId);
			return;
		}

		const segmentIds = selectedSegmentIds.includes(segmentId) ? selectedSegmentIds : [segmentId];
		setSelection(segmentIds);
		dragOperation = {
			type: 'move',
			pointerId: event.pointerId,
			segmentIds,
			start: point,
			initialPlacements: roomLayout.placements
				.filter((entry) => segmentIds.includes(entry.segmentId))
				.map((entry) => ({ ...entry }))
		};
		(event.currentTarget as Element).setPointerCapture(event.pointerId);
	}

	function startSelectionMove(event: PointerEvent) {
		event.stopPropagation();
		const point = canvasPoint(event);
		if (!point || selectedPlacements.length === 0) return;

		dragOperation = {
			type: 'move',
			pointerId: event.pointerId,
			segmentIds: selectedPlacements.map((placement) => placement.segmentId),
			start: point,
			initialPlacements: selectedPlacements.map((placement) => ({ ...placement }))
		};
		(event.currentTarget as Element).setPointerCapture(event.pointerId);
	}

	function startRotate(event: PointerEvent) {
		event.stopPropagation();
		if (!selectedBounds || selectedPlacements.length === 0) return;
		const point = canvasPoint(event);
		if (!point) return;

		dragOperation = {
			type: 'rotate',
			pointerId: event.pointerId,
			segmentIds: selectedPlacements.map((placement) => placement.segmentId),
			bounds: selectedBounds,
			startAngle: Math.atan2(point.y - selectedBounds.center.y, point.x - selectedBounds.center.x),
			currentDelta: 0,
			initialPlacements: selectedPlacements.map((placement) => ({ ...placement }))
		};
		(event.currentTarget as Element).setPointerCapture(event.pointerId);
	}

	function startScale(corner: ScaleCorner, event: PointerEvent) {
		event.stopPropagation();
		if (!selectedBounds || selectedPlacements.length === 0) return;

		dragOperation = {
			type: 'scale',
			pointerId: event.pointerId,
			segmentIds: selectedPlacements.map((placement) => placement.segmentId),
			corner,
			bounds: selectedBounds,
			initialPlacements: selectedPlacements.map((placement) => ({ ...placement }))
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
			baseSelection: event.shiftKey ? selectedSegmentIds : []
		};
		if (!event.shiftKey) {
			setSelection([]);
		}
	}

	function drag(event: PointerEvent) {
		if (marquee) {
			updateMarquee(event);
			return;
		}

		if (!dragOperation || event.pointerId !== dragOperation.pointerId) return;
		const operation = dragOperation;
		const point = canvasPoint(event);
		if (!point) return;

		if (operation.type === 'move') {
			const dx = point.x - operation.start.x;
			const dy = point.y - operation.start.y;
			updatePlacements(operation.segmentIds, (placement) => {
				const initial = operation.initialPlacements.find(
					(entry) => entry.segmentId === placement.segmentId
				);
				if (!initial) return placement;

				return {
					...initial,
					x: initial.x + dx,
					y: initial.y + dy
				};
			});
			return;
		}

		if (operation.type === 'rotate') {
			const currentAngle = Math.atan2(
				point.y - operation.bounds.center.y,
				point.x - operation.bounds.center.x
			);
			const delta = ((currentAngle - operation.startAngle) * 180) / Math.PI;
			const snappedDelta = event.shiftKey ? Math.round(delta / 15) * 15 : delta;
			const radians = (snappedDelta * Math.PI) / 180;
			const cos = Math.cos(radians);
			const sin = Math.sin(radians);
			dragOperation = { ...operation, currentDelta: snappedDelta };
			updatePlacements(operation.segmentIds, (placement) => {
				const initial = operation.initialPlacements.find(
					(entry) => entry.segmentId === placement.segmentId
				);
				if (!initial) return placement;

				const x = initial.x - operation.bounds.center.x;
				const y = initial.y - operation.bounds.center.y;
				return {
					...initial,
					x: operation.bounds.center.x + x * cos - y * sin,
					y: operation.bounds.center.y + x * sin + y * cos,
					rotation: initial.rotation + snappedDelta
				};
			});
			return;
		}

		const anchor = operation.bounds.center;
		const startCorner = cornerPoint(operation.bounds, operation.corner);
		const startDx = startCorner.x - anchor.x;
		const startDy = startCorner.y - anchor.y;
		let scaleX = Math.abs(startDx) < 0.0001 ? 1 : (point.x - anchor.x) / startDx;
		let scaleY = Math.abs(startDy) < 0.0001 ? 1 : (point.y - anchor.y) / startDy;

		if (event.shiftKey) {
			const scale = Math.max(Math.abs(scaleX), Math.abs(scaleY));
			scaleX = Math.sign(scaleX || 1) * scale;
			scaleY = Math.sign(scaleY || 1) * scale;
		}

		updatePlacements(operation.segmentIds, (placement) => {
				const initial = operation.initialPlacements.find(
					(entry) => entry.segmentId === placement.segmentId
				);
				if (!initial) return placement;

				return {
					...initial,
					x: anchor.x + (initial.x - anchor.x) * scaleX,
					y: anchor.y + (initial.y - anchor.y) * scaleY,
					scaleX: initial.scaleX * Math.abs(scaleX),
					scaleY: initial.scaleY * Math.abs(scaleY)
				};
			});
	}

	function endDrag(event: PointerEvent) {
		if (marquee) {
			finishMarquee(event);
			return;
		}

		if (!dragOperation || event.pointerId !== dragOperation.pointerId) return;

		const target = event.currentTarget as Element;
		if (target.hasPointerCapture(event.pointerId)) {
			target.releasePointerCapture(event.pointerId);
		}
		dragOperation = null;
	}

	function updateMarquee(event: PointerEvent) {
		if (!marquee || event.pointerId !== marquee.pointerId) return;
		const point = canvasPoint(event);
		if (!point) return;

		marquee = { ...marquee, current: point };
		setSelection(mergeSelections(marquee.baseSelection, placementsInMarquee(marquee.start, point)));
	}

	function finishMarquee(event: PointerEvent) {
		if (!marquee || event.pointerId !== marquee.pointerId) return;

		(event.currentTarget as Element).releasePointerCapture(event.pointerId);
		const point = canvasPoint(event);
		const selected = point ? placementsInMarquee(marquee.start, point) : [];
		const moved =
			point &&
			(Math.abs(point.x - marquee.start.x) > 0.004 || Math.abs(point.y - marquee.start.y) > 0.004);

		setSelection(moved ? mergeSelections(marquee.baseSelection, selected) : marquee.baseSelection);
		marquee = null;
	}

	function cornerPoint(bounds: PlacementBounds, corner: ScaleCorner): LedLayoutPoint {
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

	function scaleHandleCursor(corner: ScaleCorner) {
		return corner === 'nw' || corner === 'se' ? 'cursor-nwse-resize' : 'cursor-nesw-resize';
	}

	onMount(() => {
		void loadRoom();
	});
</script>

<svelte:head>
	<title>Room Editor - LucaLights</title>
	<meta name="description" content="Place LucaLights segment shapes in global room space." />
</svelte:head>

<div class="relative min-h-screen overflow-hidden bg-(image:--page-gradient) text-foreground">
	<div class="pointer-events-none absolute inset-0 bg-(image:--page-overlay)"></div>

	<section class="relative mx-auto flex max-w-7xl flex-col gap-6 px-4 py-6 sm:px-6 lg:px-8 lg:py-10">
		<div class="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
			<div class="max-w-3xl space-y-2">
				<h1 class="text-3xl font-semibold tracking-tight sm:text-4xl">Room Editor</h1>
				<p class="max-w-2xl text-sm leading-6 text-muted-foreground sm:text-base">
					Place saved segment shapes in global space so Pixel Info can expose room coordinates.
				</p>
			</div>

			<div class="flex flex-wrap items-center gap-2">
				<Button variant="outline" onclick={resetRoom} disabled={loading || saving}>
					<RotateCcw />
					Reset
				</Button>
				<Button onclick={saveRoom} disabled={!dirty || saving}>
					{#if saving}
						<Loader2 class="animate-spin" />
					{:else}
						<Save />
					{/if}
					Save Room
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
					<CardDescription>{segmentEntries.length} available · {roomLayout.placements.length} placed</CardDescription>
				</CardHeader>
				<CardContent class="max-h-[34rem] space-y-3 overflow-y-auto pr-2">
					{#if loading}
						<div class="flex items-center gap-2 rounded-2xl border border-border/70 bg-background/65 px-4 py-3 text-sm text-muted-foreground">
							<Loader2 class="size-4 animate-spin" />
							Loading room...
						</div>
					{:else if segmentEntries.length > 0}
						{#each devices as device}
							{#if device.segments.length > 0}
								<div class="space-y-2">
									<p class="px-1 text-xs font-medium uppercase tracking-[0.16em] text-muted-foreground">
										{device.name}
									</p>
									{#each device.segments as segment}
										<button
											type="button"
											class={`w-full rounded-xl border p-3 text-left transition ${
												segment.id === selectedSegmentId
													? 'border-primary/35 bg-primary/8 shadow-sm'
													: 'border-border/70 bg-background/65 hover:border-border hover:bg-background/80'
											}`}
											onclick={() =>
												placedSegmentIds.has(segment.id)
													? setSelection([segment.id])
													: addPlacement({ device, segment })}
										>
											<div class="flex items-center justify-between gap-2">
												<span class="text-sm font-semibold">{segment.name}</span>
												<Badge variant={placedSegmentIds.has(segment.id) ? 'secondary' : 'outline'}>
													{placedSegmentIds.has(segment.id) ? 'Placed' : segment.length}
												</Badge>
											</div>
											<p class="mt-2 text-xs text-muted-foreground">
												{placedSegmentIds.has(segment.id) ? 'Select in room' : 'Add to room'}
											</p>
										</button>
									{/each}
								</div>
							{/if}
						{/each}
					{:else}
						<div class="rounded-2xl border border-dashed border-border/80 bg-background/50 px-4 py-8 text-center text-sm text-muted-foreground">
							Add device segments before authoring a room layout.
						</div>
					{/if}
				</CardContent>
			</Card>

			<Card class="min-h-0 border-surface-card-border bg-surface-card-alt shadow-sm backdrop-blur">
				<CardHeader class="space-y-3">
					<div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
						<div class="space-y-1">
							<CardTitle>{selectedSegment?.name ?? 'Room space'}</CardTitle>
							<CardDescription>
								Move, rotate, and scale whole segment shapes without changing their local layout.
							</CardDescription>
						</div>
						<div class="flex flex-wrap gap-2">
							<Badge variant="outline">{roomLayout.placements.length} placements</Badge>
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
							aria-label="Room layout canvas"
							onpointermove={drag}
							onpointerup={endDrag}
							onpointercancel={endDrag}
							onpointerdown={startMarquee}
						>
							<rect x="0" y="0" width="1" height="1" fill="transparent" />
							{#each roomLayout.placements as placement (placement.segmentId)}
								{@const entry = findSegmentEntry(placement.segmentId)}
								{#if entry}
									{@const points = transformedPoints(entry.segment, placement)}
									{@const bounds = calculatePlacementBounds(entry.segment, placement)}
									<g
										role="button"
										tabindex="0"
										aria-label={`Move ${entry.segment.name}`}
										class="cursor-move"
										onpointerdown={(event) => startMove(placement.segmentId, event)}
									>
										{#if bounds}
											<rect
												x={bounds.x}
												y={bounds.y}
												width={bounds.width}
												height={bounds.height}
												fill="transparent"
											/>
										{/if}
										{#if points.length > 1}
											<polyline
												points={points.map((point) => `${point.x},${point.y}`).join(' ')}
												fill="none"
												class={selectionContains(placement.segmentId)
													? 'stroke-amber-300'
													: 'stroke-primary'}
												stroke-opacity={selectionContains(placement.segmentId) ? '0.72' : '0.32'}
												stroke-width="0.004"
											/>
										{/if}
										{#each points as point, index}
											<circle
												cx={point.x}
												cy={point.y}
												r={pointRadius}
												class={selectionContains(placement.segmentId)
													? index === 0
														? 'fill-emerald-300 stroke-background'
														: 'fill-amber-200 stroke-background'
												: index === 0
													? 'fill-emerald-400 stroke-background'
													: 'fill-primary stroke-background'}
												stroke-width="0.003"
											/>
										{/each}
									</g>
								{/if}
							{/each}

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

							{#if activeSelectionBounds}
								<g
									transform={`rotate(${activeSelectionRotation} ${activeSelectionBounds.center.x} ${activeSelectionBounds.center.y})`}
								>
									<rect
										role="button"
										tabindex="0"
										aria-label="Move selected segments"
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
										aria-label="Rotate selected segments"
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
											aria-label={`Scale selected segments from ${corner}`}
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
					<CardDescription>Room transforms affect Global X and Global Y in Pixel Info.</CardDescription>
				</CardHeader>
				<CardContent class="space-y-5">
					<div class="grid gap-2">
						<Button
							variant="outline"
							onclick={() => segmentEntries.forEach(addPlacement)}
							disabled={segmentEntries.length === roomLayout.placements.length}
						>
							<Plus />
							Add All Segments
						</Button>
						<Button variant="outline" onclick={removeSelectedPlacement} disabled={selectedPlacements.length === 0}>
							<Trash2 />
							Remove Selected
						</Button>
					</div>

					<div class="space-y-3">
						<p class="text-sm font-medium">Selected</p>
						{#if selectedPlacements.length > 1}
							<div class="space-y-2 rounded-xl border border-border/70 bg-background/65 p-3 text-sm">
								<div class="flex items-center justify-between gap-2">
									<span class="font-semibold">Multiple segments</span>
									<Badge variant="outline">{selectedPlacements.length}</Badge>
								</div>
								<p class="text-xs text-muted-foreground">
									Group move, scale, and rotate are active.
								</p>
							</div>
						{:else if selectedPlacement && selectedSegment}
							<div class="space-y-2 rounded-xl border border-border/70 bg-background/65 p-3 text-sm">
								<div class="flex items-center justify-between gap-2">
									<span class="font-semibold">{selectedSegment.name}</span>
									<Badge variant="outline">{selectedSegment.length} LEDs</Badge>
								</div>
								<p class="text-xs text-muted-foreground">
									x {selectedPlacement.x.toFixed(3)} · y {selectedPlacement.y.toFixed(3)}
								</p>
								<p class="text-xs text-muted-foreground">
									rot {selectedPlacement.rotation.toFixed(1)}° · scale {selectedPlacement.scaleX.toFixed(3)} / {selectedPlacement.scaleY.toFixed(3)}
								</p>
							</div>
						{:else}
							<div class="rounded-xl border border-dashed border-border/80 bg-background/50 px-4 py-8 text-center text-sm text-muted-foreground">
								<BoxSelect class="mx-auto mb-3 size-5" />
								Select or add a segment.
							</div>
						{/if}
					</div>

					<p class="text-sm leading-6 text-muted-foreground">
						Drag segment shapes in the canvas. Hold Shift while rotating to snap by 15 degrees, or
						while scaling to keep width and height equal.
					</p>
					<p class="flex items-center gap-2 text-sm leading-6 text-muted-foreground">
						<Grid3X3 class="size-4" />
						Local Layout X/Y still comes from the segment editor.
					</p>
				</CardContent>
			</Card>
		</div>
	</section>
</div>
