<script lang="ts">
	import { onMount, untrack } from 'svelte';
	import {
		Background,
		Controls,
		MiniMap,
		SvelteFlow,
		type Connection,
		type Edge,
		type NodeTypes,
		type Viewport
	} from '@xyflow/svelte';
	import '@xyflow/svelte/dist/style.css';
	import {
		ArrowLeft,
		Check,
		Layers3,
		Loader2,
		Plus,
		Save,
		Search,
		TriangleAlert,
		Workflow
	} from '@lucide/svelte';
	import { theme } from '$lib/theme.svelte';
	import GraphNode from '$lib/components/editor/GraphNode.svelte';
	import type {
		EditorDeviceOption,
		EditorFlowNode,
		EditorNodeData,
		EditorSegmentOption
	} from '$lib/components/editor/types';
	import { Badge } from '$lib/components/ui/badge';
	import { Button } from '$lib/components/ui/button';
	import {
		Card,
		CardContent,
		CardDescription,
		CardHeader,
		CardTitle
	} from '$lib/components/ui/card';
	import { Separator } from '$lib/components/ui/separator';
	import {
		apiGet,
		apiPut,
		toMessage,
		type Device,
		type GraphDiagnostic,
		type GraphResponse,
		type InputChannelDefinition,
		type InputDefinition,
		type NodeTypeDefinition,
		type NodeTypesResponse,
		type SvelteFlowGraphDocument,
		type SystemStatus
	} from '$lib/lucalights';

	const defaultViewport: Viewport = { x: 0, y: 0, zoom: 1 };
	const pasteOffset = { x: 42, y: 42 };

	type GraphClipboardNode = {
		id: string;
		typeId: string;
		position: { x: number; y: number };
		properties: Record<string, unknown>;
	};

	type GraphClipboardEdge = {
		source: string;
		sourceHandle?: string;
		target: string;
		targetHandle?: string;
	};

	type GraphClipboard = {
		nodes: GraphClipboardNode[];
		edges: GraphClipboardEdge[];
	};

	let nodeTypes = $state<NodeTypeDefinition[]>([]);
	let inputDefinitions = $state<InputDefinition[]>([]);
	let devices = $state<Device[]>([]);
	let activeInputModuleId = $state<string | null>(null);

	let nodes = $state<EditorFlowNode[]>([]);
	let edges = $state<Edge[]>([]);
	let viewport = $state<Viewport>(defaultViewport);

	let canvasHost = $state<HTMLDivElement | null>(null);
	let paletteFilter = $state('');
	let loading = $state(true);
	let saving = $state(false);
	let dirty = $state(false);
	let initialized = $state(false);
	let errorMessage = $state('');
	let validationDiagnostics = $state<GraphDiagnostic[]>([]);
	let lastSaveResult = $state<'success' | 'error' | null>(null);
	let graphClipboard = $state<GraphClipboard | null>(null);
	let clipboardPasteCount = $state(0);

	let nodeTypeMap = $derived(new Map(nodeTypes.map((nodeType) => [nodeType.typeId, nodeType])));
	let activeInputDefinition = $derived(
		inputDefinitions.find((definition) => definition.moduleId === activeInputModuleId) ?? null
	);
	let flowNodeTypes = $derived.by(
		() =>
			Object.fromEntries(
				nodeTypes.map((nodeType) => [nodeType.typeId, GraphNode])
			) as NodeTypes
	);
	let validationErrors = $derived(
		validationDiagnostics
			.filter((diagnostic) => diagnostic.severity === 'Error')
			.map((diagnostic) => diagnostic.message)
	);
	let validationWarnings = $derived(
		validationDiagnostics
			.filter((diagnostic) => diagnostic.severity !== 'Error')
			.map((diagnostic) => diagnostic.message)
	);
	let paletteGroups = $derived.by(() => {
		const filter = paletteFilter.trim().toLowerCase();
		const groups = new Map<string, NodeTypeDefinition[]>();

		for (const nodeType of nodeTypes) {
			const matchesFilter =
				filter.length === 0 ||
				nodeType.displayName.toLowerCase().includes(filter) ||
				nodeType.description.toLowerCase().includes(filter) ||
				nodeType.typeId.toLowerCase().includes(filter) ||
				nodeType.category.toLowerCase().includes(filter);

			if (!matchesFilter) {
				continue;
			}

			const existingGroup = groups.get(nodeType.category);
			if (existingGroup) {
				existingGroup.push(nodeType);
			} else {
				groups.set(nodeType.category, [nodeType]);
			}
		}

		return Array.from(groups.entries()).map(([category, items]) => ({ category, items }));
	});
	let connectedInputSignature = $derived.by(() =>
		edges
			.map((edge) => `${edge.target}:${edge.targetHandle ?? ''}`)
			.sort()
			.join('|')
	);

	function buildDeviceOptions() {
		return devices.map(
			(device): EditorDeviceOption => ({
				id: device.id,
				name: device.name
			})
		);
	}

	function buildSegmentOptions() {
		return devices.flatMap((device) =>
			device.segments.map(
				(segment): EditorSegmentOption => ({
					id: segment.id,
					name: segment.name,
					deviceId: device.id,
					deviceName: device.name
				})
			)
		);
	}

	function buildInputChannelOptions() {
		if (activeInputDefinition) {
			return activeInputDefinition.channels.map((channel) => ({
				...channel,
				moduleId: activeInputDefinition.moduleId,
				moduleDisplayName: activeInputDefinition.displayName
			}));
		}

		const channelsByKey = new Map<string, InputChannelDefinition>();
		for (const definition of inputDefinitions) {
			for (const channel of definition.channels) {
				if (!channelsByKey.has(channel.key)) {
					channelsByKey.set(channel.key, {
						...channel,
						moduleId: definition.moduleId,
						moduleDisplayName: definition.displayName
					});
				}
			}
		}

		return Array.from(channelsByKey.values());
	}

	function cloneProperties(properties: Record<string, unknown>) {
		return Object.fromEntries(
			Object.entries(properties).map(([key, value]) => [key, value])
		);
	}

	function connectedInputIdsFor(nodeId: string, edgeList: Edge[] = edges) {
		return Array.from(
			new Set(
				edgeList
					.filter((edge) => edge.target === nodeId && typeof edge.targetHandle === 'string')
					.map((edge) => edge.targetHandle as string)
			)
		);
	}

	function defaultPropertiesFor(nodeType: NodeTypeDefinition) {
		const properties: Record<string, unknown> = {};

		for (const property of nodeType.properties) {
			if (property.defaultValue !== undefined && property.defaultValue !== null) {
				properties[property.key] = property.defaultValue;
			} else if (property.valueType === 'Bool') {
				properties[property.key] = false;
			} else if (property.valueType === 'Float') {
				properties[property.key] = 0;
			} else {
				properties[property.key] = '';
			}
		}

		return properties;
	}

	function createNodeData(
		nodeId: string,
		nodeType: NodeTypeDefinition | undefined,
		properties: Record<string, unknown>,
		edgeList: Edge[] = edges
	): EditorNodeData {
		const label =
			nodeType?.typeId === 'annotation.comment' &&
			typeof properties.title === 'string' &&
			properties.title.trim().length > 0
				? properties.title.trim()
				: nodeType?.displayName ?? nodeId;

		return {
			label,
			typeId: nodeType?.typeId ?? 'unknown',
			category: nodeType?.category ?? 'Unknown',
			description: nodeType?.description ?? 'Unknown node type.',
			properties: cloneProperties(properties),
			propertyDefs: nodeType?.properties ?? [],
			inputs: nodeType?.inputs ?? [],
			outputs: nodeType?.outputs ?? [],
			connectedInputIds: connectedInputIdsFor(nodeId, edgeList),
			inputChannelOptions: buildInputChannelOptions(),
			deviceOptions: buildDeviceOptions(),
			segmentOptions: buildSegmentOptions(),
			onPropertyChange: updateNodeProperty
		};
	}

	function graphDocumentToFlow(
		graph: SvelteFlowGraphDocument,
		typeMap: Map<string, NodeTypeDefinition>
	) {
		const mappedEdges = graph.edges.map(
			(edge): Edge => ({
				id: edge.id,
				source: edge.source,
				sourceHandle: edge.sourceHandle || undefined,
				target: edge.target,
				targetHandle: edge.targetHandle || undefined,
				animated: false
			})
		);

		return {
			nodes: graph.nodes.map(
				(graphNode): EditorFlowNode => ({
					id: graphNode.id,
					type: graphNode.type,
					position: graphNode.position,
					data: createNodeData(
						graphNode.id,
						typeMap.get(graphNode.type),
						graphNode.data.properties ?? {},
						mappedEdges
					)
				})
			),
			edges: mappedEdges,
			viewport: graph.viewport ?? defaultViewport
		};
	}

	function flowToGraphDocument(): SvelteFlowGraphDocument {
		return {
			nodes: nodes.map((node) => ({
				id: node.id,
				type: node.data.typeId,
				position: node.position,
				data: {
					properties: node.data.properties
				}
			})),
			edges: edges.map((edge) => ({
				id: edge.id,
				source: edge.source,
				sourceHandle: edge.sourceHandle ?? '',
				target: edge.target,
				targetHandle: edge.targetHandle ?? ''
			})),
			viewport
		};
	}

	function markDirty() {
		if (initialized) {
			dirty = true;
			lastSaveResult = null;
		}
	}

	function updateNodeProperty(nodeId: string, key: string, value: unknown) {
		nodes = nodes.map((node) =>
			node.id === nodeId
				? (() => {
						const nextProperties = {
							...node.data.properties,
							[key]: value
						};
						const nodeType = nodeTypeMap.get(node.data.typeId);
						const nextLabel =
							node.data.typeId === 'annotation.comment' &&
							typeof nextProperties.title === 'string' &&
							nextProperties.title.trim().length > 0
								? nextProperties.title.trim()
								: nodeType?.displayName ?? node.id;

						return {
							...node,
							data: {
								...node.data,
								label: nextLabel,
								properties: nextProperties
							}
						};
					})()
				: node
		);
		markDirty();
	}

	function refreshConnectedInputs(edgeList: Edge[] = edges) {
		let changed = false;

		const nextNodes = nodes.map((node) => {
			const nextConnectedInputIds = connectedInputIdsFor(node.id, edgeList);
			const previousConnectedInputIds = node.data.connectedInputIds ?? [];
			const isSame =
				previousConnectedInputIds.length === nextConnectedInputIds.length &&
				previousConnectedInputIds.every((value, index) => value === nextConnectedInputIds[index]);

			if (isSame) {
				return node;
			}

			changed = true;
			return {
				...node,
				data: {
					...node.data,
					connectedInputIds: nextConnectedInputIds
				}
			};
		});

		if (changed) {
			nodes = nextNodes;
		}
	}

	function createNodeId(typeId: string) {
		const stem = typeId.split('.').pop()?.replace(/[^a-z0-9]+/gi, '-').toLowerCase() ?? 'node';
		const suffix =
			typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
				? crypto.randomUUID().slice(0, 8)
				: Math.random().toString(36).slice(2, 10);

		return `${stem}-${suffix}`;
	}

	function clientToFlowPosition(clientX: number, clientY: number) {
		const bounds = canvasHost?.getBoundingClientRect();
		if (!bounds) {
			return { x: 40, y: 40 };
		}

		const zoom = viewport.zoom || 1;
		return {
			x: (clientX - bounds.left - viewport.x) / zoom,
			y: (clientY - bounds.top - viewport.y) / zoom
		};
	}

	function canvasCenterPosition() {
		const bounds = canvasHost?.getBoundingClientRect();
		if (!bounds) {
			return { x: 80 + nodes.length * 20, y: 80 + nodes.length * 20 };
		}

		return clientToFlowPosition(bounds.left + bounds.width / 2, bounds.top + bounds.height / 2);
	}

	function addNodeFromType(typeId: string, position = canvasCenterPosition()) {
		const nodeType = nodeTypeMap.get(typeId);
		if (!nodeType) {
			return;
		}

		const nodeId = createNodeId(typeId);
		const nextNode: EditorFlowNode = {
			id: nodeId,
			type: typeId,
			position: {
				x: position.x + nodes.length * 8,
				y: position.y + nodes.length * 8
			},
			data: createNodeData(nodeId, nodeType, defaultPropertiesFor(nodeType))
		};

		nodes = [...nodes, nextNode];
		markDirty();
	}

	function getPort(
		node: EditorFlowNode | undefined,
		handleId: string | undefined,
		direction: 'input' | 'output'
	) {
		if (!node || !handleId) {
			return null;
		}

		const collection = direction === 'input' ? node.data.inputs : node.data.outputs;
		return collection.find((port) => port.id === handleId) ?? null;
	}

	function conflictingTargetEdges(
		connection: { target: string | null | undefined; targetHandle?: string | null | undefined },
		excludedEdgeIds: string[] = [],
		edgeList: Edge[] = edges
	) {
		const targetHandle = connection.targetHandle ?? undefined;
		if (!connection.target || !targetHandle) {
			return [];
		}

		return edgeList.filter(
			(edge) =>
				edge.target === connection.target &&
				edge.targetHandle === targetHandle &&
				!excludedEdgeIds.includes(edge.id)
		);
	}

	function createEdgeFromConnection(connection: Connection): Edge {
		return {
			...connection,
			id: createNodeId('edge'),
			animated: false
		};
	}

	function resolveTargetPort(connection: {
		target: string | null | undefined;
		targetHandle?: string | null | undefined;
	}) {
		const targetHandle = connection.targetHandle ?? undefined;
		const targetNode = nodes.find((node) => node.id === connection.target);
		return getPort(targetNode, targetHandle, 'input');
	}

	function removeConflictingTargetEdges(
		connection: { target: string | null | undefined; targetHandle?: string | null | undefined },
		excludedEdgeIds: string[] = []
	) {
		const conflicts = conflictingTargetEdges(connection, excludedEdgeIds);
		if (conflicts.length === 0) {
			return;
		}

		edges = edges.filter((edge) => !conflicts.some((conflict) => conflict.id === edge.id));
	}

	function isValidConnection(connection: Connection | Edge) {
		const sourceHandle = connection.sourceHandle ?? undefined;
		const targetHandle = connection.targetHandle ?? undefined;

		if (!connection.source || !connection.target || !sourceHandle || !targetHandle) {
			return false;
		}

		if (connection.source === connection.target) {
			return false;
		}

		const sourceNode = nodes.find((node) => node.id === connection.source);
		const targetNode = nodes.find((node) => node.id === connection.target);
		const sourcePort = getPort(sourceNode, sourceHandle, 'output');
		const targetPort = getPort(targetNode, targetHandle, 'input');

		if (!sourcePort || !targetPort) {
			return false;
		}

		if (sourcePort.valueType !== targetPort.valueType) {
			return false;
		}

		if (
			edges.some(
				(edge) =>
					edge.source === connection.source &&
					edge.sourceHandle === sourceHandle &&
					edge.target === connection.target &&
					edge.targetHandle === targetHandle
			)
		) {
			return false;
		}

		return true;
	}

	async function loadGraph() {
		loading = true;
		initialized = false;
		errorMessage = '';

		try {
			const [graphData, nodeTypesData, moduleDefinitions, deviceList, systemStatus] =
				await Promise.all([
					apiGet<GraphResponse>('/api/graph'),
					apiGet<NodeTypesResponse>('/api/node-types'),
					apiGet<InputDefinition[]>('/api/input-modules'),
					apiGet<Device[]>('/api/devices'),
					apiGet<SystemStatus>('/api/system/status')
				]);

			nodeTypes = nodeTypesData.nodeTypes;
			inputDefinitions = moduleDefinitions;
			devices = deviceList;
			activeInputModuleId = systemStatus.input.activeModuleId;

			const typeMap = new Map(nodeTypesData.nodeTypes.map((nodeType) => [nodeType.typeId, nodeType]));
			const flow = graphDocumentToFlow(graphData.graph, typeMap);
			nodes = flow.nodes;
			edges = flow.edges;
			viewport = flow.viewport;
			validationDiagnostics = graphData.validation.diagnostics;
			dirty = false;
			lastSaveResult = null;

			requestAnimationFrame(() => {
				initialized = true;
			});
		} catch (error) {
			errorMessage = toMessage(error);
		} finally {
			loading = false;
		}
	}

	async function saveGraph() {
		saving = true;
		lastSaveResult = null;

		try {
			const result = await apiPut<GraphResponse>('/api/graph', flowToGraphDocument());

			validationDiagnostics = result.validation.diagnostics;
			dirty = false;
			lastSaveResult = 'success';
		} catch (error) {
			errorMessage = toMessage(error);
			lastSaveResult = 'error';
		} finally {
			saving = false;
		}
	}

	function handleBeforeConnect(connection: Connection) {
		if (!isValidConnection(connection)) {
			return false;
		}

		const targetPort = resolveTargetPort(connection);
		if (!targetPort) {
			return false;
		}

		if (targetPort.allowMultipleConnections !== true) {
			removeConflictingTargetEdges(connection);
		}

		return createEdgeFromConnection(connection);
	}

	function handleConnect(_connection: Connection) {
		markDirty();
	}

	function handleBeforeReconnect(nextEdge: Edge, previousEdge: Edge) {
		if (!isValidConnection(nextEdge)) {
			return false;
		}

		const targetPort = resolveTargetPort(nextEdge);
		if (!targetPort) {
			return false;
		}

		if (targetPort.allowMultipleConnections !== true) {
			removeConflictingTargetEdges(nextEdge, [previousEdge.id]);
		}

		return {
			...nextEdge,
			id: previousEdge.id,
			animated: false
		};
	}

	function handleReconnect(_previousEdge: Edge, _nextConnection: Connection) {
		markDirty();
	}

	function handleDelete() {
		markDirty();
	}

	function handleNodeDragStop() {
		markDirty();
	}

	function handleMoveEnd(_event: MouseEvent | TouchEvent | null, nextViewport: Viewport) {
		viewport = nextViewport;
		markDirty();
	}

	function handlePaletteDragStart(event: DragEvent, typeId: string) {
		event.dataTransfer?.setData('application/x-lucalights-node-type', typeId);
		event.dataTransfer?.setData('text/plain', typeId);
		if (event.dataTransfer) {
			event.dataTransfer.effectAllowed = 'copy';
		}
	}

	function handleCanvasDragOver(event: DragEvent) {
		event.preventDefault();
		if (event.dataTransfer) {
			event.dataTransfer.dropEffect = 'copy';
		}
	}

	function handleCanvasDrop(event: DragEvent) {
		event.preventDefault();
		const typeId = event.dataTransfer?.getData('application/x-lucalights-node-type');
		if (!typeId) {
			return;
		}

		addNodeFromType(typeId, clientToFlowPosition(event.clientX, event.clientY));
	}

	function isEditableTarget(target: EventTarget | null) {
		if (!(target instanceof HTMLElement)) {
			return false;
		}

		if (target.isContentEditable) {
			return true;
		}

		return !!target.closest('input, textarea, select, [contenteditable="true"], [role="textbox"]');
	}

	function selectedNodes() {
		return nodes.filter((node) => node.selected);
	}

	function copySelectionToClipboard() {
		const selected = selectedNodes();
		if (selected.length === 0) {
			return false;
		}

		const selectedIdSet = new Set(selected.map((node) => node.id));
		const internalEdges = edges
			.filter((edge) => selectedIdSet.has(edge.source) && selectedIdSet.has(edge.target))
			.map((edge): GraphClipboardEdge => ({
				source: edge.source,
				sourceHandle: edge.sourceHandle ?? undefined,
				target: edge.target,
				targetHandle: edge.targetHandle ?? undefined
			}));

		graphClipboard = {
			nodes: selected.map((node) => ({
				id: node.id,
				typeId: node.data.typeId,
				position: {
					x: node.position.x,
					y: node.position.y
				},
				properties: cloneProperties(node.data.properties)
			})),
			edges: internalEdges
		};
		clipboardPasteCount = 0;
		return true;
	}

	function pasteClipboardSelection() {
		if (!graphClipboard || graphClipboard.nodes.length === 0) {
			return false;
		}

		clipboardPasteCount += 1;
		const offsetX = pasteOffset.x * clipboardPasteCount;
		const offsetY = pasteOffset.y * clipboardPasteCount;
		const nodeIdMap = new Map<string, string>();

		const pastedNodes = graphClipboard.nodes.map((clipboardNode) => {
			const nodeType = nodeTypeMap.get(clipboardNode.typeId);
			const nodeId = createNodeId(clipboardNode.typeId);
			nodeIdMap.set(clipboardNode.id, nodeId);

			return {
				id: nodeId,
				type: clipboardNode.typeId,
				position: {
					x: clipboardNode.position.x + offsetX,
					y: clipboardNode.position.y + offsetY
				},
				selected: true,
				data: createNodeData(
					nodeId,
					nodeType,
					cloneProperties(clipboardNode.properties)
				)
			} satisfies EditorFlowNode;
		});

		const pastedEdges = graphClipboard.edges.flatMap((clipboardEdge) => {
			const source = nodeIdMap.get(clipboardEdge.source);
			const target = nodeIdMap.get(clipboardEdge.target);

			if (!source || !target) {
				return [];
			}

			const nextEdge: Edge = {
				id: createNodeId('edge'),
				source,
				sourceHandle: clipboardEdge.sourceHandle ?? undefined,
				target,
				targetHandle: clipboardEdge.targetHandle ?? undefined,
				animated: false,
				selected: true
			};

			return isValidConnection(nextEdge) ? [nextEdge] : [];
		});

		nodes = [
			...nodes.map((node) => ({
				...node,
				selected: false
			})),
			...pastedNodes
		];
		edges = [
			...edges.map((edge) => ({
				...edge,
				selected: false
			})),
			...pastedEdges
		];
		markDirty();
		return true;
	}

	function handleKeydown(event: KeyboardEvent) {
		if (!(event.ctrlKey || event.metaKey) || isEditableTarget(event.target)) {
			return;
		}

		const key = event.key.toLowerCase();

		if (key === 's') {
			event.preventDefault();
			if (!saving && dirty) {
				void saveGraph();
			}
			return;
		}

		if (key === 'c') {
			if (copySelectionToClipboard()) {
				event.preventDefault();
			}
			return;
		}

		if (key === 'v') {
			if (pasteClipboardSelection()) {
				event.preventDefault();
			}
			return;
		}
	}

	onMount(() => {
		void loadGraph();
	});

	$effect(() => {
		connectedInputSignature;
		const edgeSnapshot = edges;

		untrack(() => {
			refreshConnectedInputs(edgeSnapshot);
		});
	});
</script>

<svelte:head>
	<title>Graph Editor - LucaLights</title>
</svelte:head>

<svelte:window onkeydown={handleKeydown} />

<div class="flex h-[calc(100vh-3.5rem)] flex-col">
	<div class="flex items-center justify-between border-b border-border/60 bg-background/80 px-4 py-2 backdrop-blur-lg">
		<div class="flex items-center gap-3">
			<Button variant="ghost" size="sm" href="/">
				<ArrowLeft class="size-4" />
				Dashboard
			</Button>

			<span class="text-sm font-semibold">Graph Editor</span>

			<Badge variant="outline">
				<Layers3 class="size-3" />
				{nodes.length} nodes / {edges.length} edges
			</Badge>

			{#if activeInputDefinition}
				<Badge variant="outline">{activeInputDefinition.displayName}</Badge>
			{/if}

			{#if dirty}
				<Badge variant="secondary">Unsaved changes</Badge>
			{/if}

			{#if lastSaveResult === 'success'}
				<Badge variant="default">
					<Check class="size-3" />
					Saved
				</Badge>
			{/if}
		</div>

		<div class="flex items-center gap-2">
			{#if validationErrors.length > 0}
				<Badge variant="destructive">
					<TriangleAlert class="size-3" />
					{validationErrors.length} error{validationErrors.length !== 1 ? 's' : ''}
				</Badge>
			{/if}

			<Button size="sm" onclick={saveGraph} disabled={saving || !dirty}>
				{#if saving}
					<Loader2 class="size-4 animate-spin" />
				{:else}
					<Save class="size-4" />
				{/if}
				Save
			</Button>
		</div>
	</div>

	{#if errorMessage}
		<div class="border-b border-destructive/30 bg-destructive/10 px-4 py-2 text-sm text-destructive">
			{errorMessage}
		</div>
	{/if}

	<div class="grid min-h-0 flex-1 xl:grid-cols-[20rem_minmax(0,1fr)]">
		<aside class="overflow-auto border-r border-border/60 bg-(image:--editor-sidebar)">
			<div class="space-y-5 p-4">
				<div class="space-y-2">
					<h2 class="text-sm font-semibold tracking-tight">Node Palette</h2>
					<p class="text-sm leading-5 text-muted-foreground">
						Drag a node onto the canvas to place it exactly where you want it.
					</p>
				</div>

				<label class="relative block">
					<Search class="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
					<input
						class="h-10 w-full rounded-xl border border-border/70 bg-surface-glass pl-9 pr-3 text-sm shadow-sm outline-none transition focus:border-ring focus:ring-4 focus:ring-ring/20"
						bind:value={paletteFilter}
						placeholder="Search nodes"
					/>
				</label>

				<div class="grid gap-3">
					{#each paletteGroups as group}
						<div class="space-y-2">
							<div class="flex items-center justify-between gap-3">
								<h3 class="text-[11px] font-semibold uppercase tracking-[0.2em] text-muted-foreground">
									{group.category}
								</h3>
								<Badge variant="outline">{group.items.length}</Badge>
							</div>

							<div class="space-y-2">
								{#each group.items as nodeType}
									<button
										type="button"
										draggable="true"
										class="w-full cursor-grab rounded-2xl border border-border/70 bg-surface-glass p-3 text-left shadow-sm transition hover:border-primary/40 hover:bg-surface-glass-hover active:cursor-grabbing"
										ondragstart={(event) => handlePaletteDragStart(event, nodeType.typeId)}
									>
										<div class="flex items-start justify-between gap-3">
											<div class="space-y-1">
												<p class="text-sm font-semibold">{nodeType.displayName}</p>
												<p class="text-xs leading-5 text-muted-foreground">
													{nodeType.description}
												</p>
											</div>
											<Plus class="size-4 text-muted-foreground" />
										</div>
									</button>
								{/each}
							</div>
						</div>
					{/each}

					{#if paletteGroups.length === 0}
						<div class="rounded-2xl border border-dashed border-border/80 bg-surface-card px-4 py-8 text-center text-sm text-muted-foreground">
							No nodes match the current filter.
						</div>
					{/if}
				</div>

				<Separator />

				<div class="space-y-2 text-sm text-muted-foreground">
					<p>{buildInputChannelOptions().length} input channels available for graph input nodes.</p>
					<p>{devices.length} devices and {devices.reduce((total, device) => total + device.segments.length, 0)} segments available for output targeting.</p>
				</div>
			</div>
		</aside>

		<div
			bind:this={canvasHost}
			role="presentation"
			aria-label="Graph canvas"
			class="relative min-h-0 bg-(image:--editor-canvas)"
			ondragover={handleCanvasDragOver}
			ondrop={handleCanvasDrop}
		>
			{#if loading}
				<div class="flex h-full items-center justify-center">
					<div class="flex flex-col items-center gap-3 text-muted-foreground">
						<Loader2 class="size-8 animate-spin" />
						<p class="text-sm">Loading graph...</p>
					</div>
				</div>
			{:else}
				<SvelteFlow
					bind:nodes
					bind:edges
					bind:viewport
					nodeTypes={flowNodeTypes}
					colorMode={theme.resolved}
					onbeforeconnect={handleBeforeConnect}
					onbeforereconnect={handleBeforeReconnect}
					onconnect={handleConnect}
					ondelete={handleDelete}
					onnodedragstop={handleNodeDragStop}
					onmoveend={handleMoveEnd}
					onreconnect={handleReconnect}
					isValidConnection={isValidConnection}
					class="h-full bg-transparent"
				>
					<Background />
					<Controls />
					<MiniMap />
				</SvelteFlow>

				{#if nodes.length === 0 && edges.length === 0}
					<div class="pointer-events-none absolute inset-0 flex items-center justify-center p-6">
						<Card class="max-w-md border-surface-card-border bg-surface-glass shadow-xl">
							<CardHeader>
								<CardTitle class="flex items-center gap-2">
									<Workflow class="size-5" />
									Empty Graph
								</CardTitle>
								<CardDescription>
									Start by adding constants, inputs, logic, and output nodes from the palette.
								</CardDescription>
							</CardHeader>
							<CardContent class="text-sm text-muted-foreground">
								Drop nodes anywhere on the canvas, then connect matching handle colors to build the lighting pipeline.
							</CardContent>
						</Card>
					</div>
				{/if}
			{/if}
		</div>
	</div>

	{#if validationErrors.length > 0 || validationWarnings.length > 0}
		<div class="border-t border-border/70 bg-background/95 px-4 py-3">
			<div class="flex flex-wrap gap-2">
				{#each validationErrors as error}
					<span class="rounded-full bg-destructive/10 px-3 py-1 text-xs text-destructive">
						{error}
					</span>
				{/each}
				{#each validationWarnings as warning}
					<span class="rounded-full bg-secondary px-3 py-1 text-xs text-secondary-foreground">
						{warning}
					</span>
				{/each}
			</div>
		</div>
	{/if}
</div>
