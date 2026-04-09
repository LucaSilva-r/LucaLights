<script lang="ts">
	import { onMount } from 'svelte';
	import {
		SvelteFlow,
		Background,
		Controls,
		MiniMap,
		type Node,
		type Edge,
		type Connection,
		Position
	} from '@xyflow/svelte';
	import '@xyflow/svelte/dist/style.css';
	import {
		ArrowLeft,
		Check,
		Loader2,
		Save,
		TriangleAlert,
		Workflow
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
		type GraphResponse,
		type NodeTypeDefinition,
		type NodeTypesResponse,
		type SvelteFlowGraphDocument
	} from '$lib/lucalights';

	let nodeTypes = $state<NodeTypeDefinition[]>([]);
	let nodeTypeMap = $derived(new Map(nodeTypes.map((nt) => [nt.typeId, nt])));

	let nodes = $state<Node[]>([]);
	let edges = $state<Edge[]>([]);

	let loading = $state(true);
	let saving = $state(false);
	let dirty = $state(false);
	let initialized = $state(false);
	let errorMessage = $state('');
	let validationErrors = $state<string[]>([]);
	let lastSaveResult = $state<'success' | 'error' | null>(null);

	function graphDocumentToFlow(graph: SvelteFlowGraphDocument): { nodes: Node[]; edges: Edge[] } {
		return {
			nodes: graph.nodes.map((n) => {
				const typeDef = nodeTypeMap.get(n.type);
				return {
					id: n.id,
					type: 'default',
					position: n.position,
					data: {
						label: typeDef?.displayName ?? n.type,
						typeId: n.type,
						category: typeDef?.category ?? 'Unknown',
						properties: n.data.properties ?? {},
						inputs: typeDef?.inputs ?? [],
						outputs: typeDef?.outputs ?? []
					},
					sourcePosition: Position.Right,
					targetPosition: Position.Left
				};
			}),
			edges: graph.edges.map((e) => ({
				id: e.id,
				source: e.source,
				sourceHandle: e.sourceHandle || undefined,
				target: e.target,
				targetHandle: e.targetHandle || undefined,
				animated: true
			}))
		};
	}

	function flowToGraphDocument(): SvelteFlowGraphDocument {
		return {
			nodes: nodes.map((n) => ({
				id: n.id,
				type: n.data.typeId as string,
				position: n.position,
				data: {
					properties: (n.data.properties as Record<string, unknown>) ?? {}
				}
			})),
			edges: edges.map((e) => ({
				id: e.id,
				source: e.source,
				sourceHandle: e.sourceHandle ?? '',
				target: e.target,
				targetHandle: e.targetHandle ?? ''
			})),
			viewport: { x: 0, y: 0, zoom: 1 }
		};
	}

	function markDirty() {
		if (initialized) {
			dirty = true;
			lastSaveResult = null;
		}
	}

	async function loadGraph() {
		loading = true;
		initialized = false;
		errorMessage = '';

		try {
			const [graphData, nodeTypesData] = await Promise.all([
				apiGet<GraphResponse>('/api/graph'),
				apiGet<NodeTypesResponse>('/api/node-types')
			]);

			nodeTypes = nodeTypesData.nodeTypes;

			const flow = graphDocumentToFlow(graphData.graph);
			nodes = flow.nodes;
			edges = flow.edges;

			validationErrors = graphData.validation.diagnostics
				.filter((d) => d.severity === 'Error')
				.map((d) => d.message);

			dirty = false;

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
		validationErrors = [];

		try {
			const graphDoc = flowToGraphDocument();
			const result = await apiPut<GraphResponse>('/api/graph', graphDoc);

			validationErrors = result.validation.diagnostics
				.filter((d) => d.severity === 'Error')
				.map((d) => d.message);

			dirty = false;
			lastSaveResult = 'success';
		} catch (error) {
			errorMessage = toMessage(error);
			lastSaveResult = 'error';
		} finally {
			saving = false;
		}
	}

	function handleConnect(connection: Connection) {
		const newEdge: Edge = {
			id: `e-${connection.source}-${connection.sourceHandle ?? 'out'}-${connection.target}-${connection.targetHandle ?? 'in'}`,
			source: connection.source,
			sourceHandle: connection.sourceHandle ?? undefined,
			target: connection.target,
			targetHandle: connection.targetHandle ?? undefined,
			animated: true
		};

		edges = [...edges, newEdge];
		markDirty();
	}

	function handleDelete() {
		markDirty();
	}

	function handleNodeDragStop() {
		markDirty();
	}

	function handleKeydown(event: KeyboardEvent) {
		if ((event.ctrlKey || event.metaKey) && event.key === 's') {
			event.preventDefault();
			if (!saving && dirty) {
				saveGraph();
			}
		}
	}

	onMount(() => {
		loadGraph();
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

	{#if loading}
		<div class="flex flex-1 items-center justify-center">
			<div class="flex flex-col items-center gap-3 text-muted-foreground">
				<Loader2 class="size-8 animate-spin" />
				<p class="text-sm">Loading graph...</p>
			</div>
		</div>
	{:else if nodes.length === 0 && edges.length === 0}
		<div class="flex flex-1 items-center justify-center bg-zinc-50">
			<Card class="max-w-md">
				<CardHeader>
					<CardTitle class="flex items-center gap-2">
						<Workflow class="size-5" />
						Empty Graph
					</CardTitle>
					<CardDescription>
						No nodes in the graph yet. Add nodes to start building your lighting
						pipeline.
					</CardDescription>
				</CardHeader>
				<CardContent>
					<div class="space-y-2 text-sm text-muted-foreground">
						<p>Available node types:</p>
						<div class="flex flex-wrap gap-1.5">
							{#each nodeTypes as nodeType}
								<Badge variant="outline">{nodeType.displayName}</Badge>
							{/each}
						</div>
					</div>
				</CardContent>
			</Card>
		</div>
	{:else}
		<div class="flex-1">
			<SvelteFlow
				bind:nodes
				bind:edges
				onconnect={handleConnect}
				ondelete={handleDelete}
				onnodedragstop={handleNodeDragStop}
				fitView
				class="bg-zinc-50"
			>
				<Background />
				<Controls />
				<MiniMap />
			</SvelteFlow>
		</div>
	{/if}

	{#if validationErrors.length > 0}
		<div class="border-t border-destructive/30 bg-destructive/5 px-4 py-2">
			<div class="flex flex-wrap gap-2">
				{#each validationErrors as error}
					<span class="text-xs text-destructive">{error}</span>
				{/each}
			</div>
		</div>
	{/if}
</div>
