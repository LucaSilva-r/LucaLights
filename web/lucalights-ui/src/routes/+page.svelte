<script lang="ts">
	import {
		Activity,
		Cable,
		Cpu,
		Gamepad2,
		Layers3,
		RefreshCw,
		RotateCcw,
		Workflow
	} from "@lucide/svelte";
	import { onMount } from "svelte";
	import LiveInputPreview from "$lib/components/LiveInputPreview.svelte";
	import PreviewStrip from "$lib/components/PreviewStrip.svelte";
	import { Badge } from "$lib/components/ui/badge";
	import { Button } from "$lib/components/ui/button";
	import {
		Card,
		CardAction,
		CardContent,
		CardDescription,
		CardHeader,
		CardTitle
	} from "$lib/components/ui/card";
	import {
		apiGet,
		apiPost,
		createSocket,
		protocolLabel,
		toMessage,
		type Device,
		type InputDefinition,
		type InputSnapshot,
		type NodeTypesResponse,
		type PreviewPayload,
		type RuntimeEnvelope,
		type SystemStatus
	} from "$lib/lucalights";

	let systemStatus = $state<SystemStatus | null>(null);
	let inputSnapshot = $state<InputSnapshot | null>(null);
	let inputModules = $state<InputDefinition[]>([]);
	let devices = $state<Device[]>([]);
	let previewPayload = $state<PreviewPayload | null>(null);
	let nodeTypeCount = $state(0);
	let latestRuntimeEvent = $state<RuntimeEnvelope | null>(null);
	let eventsConnected = $state(false);
	let previewConnected = $state(false);
	let refreshing = $state(false);
	let restarting = $state(false);
	let errorMessage = $state("");

	let previewDevices = $derived(previewPayload?.devices ?? []);
	let previewDeviceCount = $derived(previewDevices.length);
	let activeModuleDefinition = $derived(
		inputModules.find((moduleDefinition) => moduleDefinition.moduleId === systemStatus?.input.activeModuleId)
	);
	let inputConnected = $derived(inputSnapshot?.isConnected ?? systemStatus?.input.connected ?? false);

	async function refreshDashboard() {
		refreshing = true;

		try {
			const [status, snapshot, moduleDefinitions, deviceList, nodeTypes] =
				await Promise.all([
					apiGet<SystemStatus>("/api/system/status"),
					apiGet<InputSnapshot>("/api/input-state"),
					apiGet<InputDefinition[]>("/api/input-modules"),
					apiGet<Device[]>("/api/devices"),
					apiGet<NodeTypesResponse>("/api/node-types")
				]);

			systemStatus = status;
			inputSnapshot = snapshot;
			inputModules = moduleDefinitions;
			devices = deviceList;
			nodeTypeCount = nodeTypes.nodeTypes.length;
			errorMessage = "";
		} catch (error) {
			errorMessage = toMessage(error);
		} finally {
			refreshing = false;
		}
	}

	async function restartEngine() {
		restarting = true;

		try {
			await apiPost("/api/system/restart-engine");
			await refreshDashboard();
		} catch (error) {
			errorMessage = toMessage(error);
		} finally {
			restarting = false;
		}
	}

	function applySnapshot(snapshot: InputSnapshot) {
		inputSnapshot = snapshot;

		if (!systemStatus) {
			return;
		}

		systemStatus = {
			...systemStatus,
			input: {
				...systemStatus.input,
				active: snapshot.isActive,
				connected: snapshot.isConnected,
				sequence: snapshot.sequence,
				timestampUtc: snapshot.timestampUtc
			}
		};
	}

	function handleRuntimeEvent(envelope: RuntimeEnvelope) {
		latestRuntimeEvent = envelope;

		switch (envelope.type) {
			case "events.connected":
				eventsConnected = true;
				return;
			case "input.snapshot":
				applySnapshot(envelope.payload as InputSnapshot);
				return;
			case "input.moduleChanged": {
				const payload = envelope.payload as { activeModuleId?: string | null };

				if (systemStatus) {
					systemStatus = {
						...systemStatus,
						input: {
							...systemStatus.input,
							activeModuleId: payload.activeModuleId ?? null
						},
						settings: {
							...systemStatus.settings,
							activeInputModuleId:
								payload.activeModuleId ?? systemStatus.settings.activeInputModuleId
						}
					};
				}

				return;
			}
			case "settings.changed":
				void refreshDashboard();
				return;
			case "system.event": {
				const payload = envelope.payload as { reason?: string };

				if (payload.reason === "engine.restarted" || payload.reason === "settings.applied") {
					void refreshDashboard();
				}

				return;
			}
		}
	}

	function handlePreviewEvent(envelope: RuntimeEnvelope) {
		switch (envelope.type) {
			case "preview.connected":
				previewConnected = true;
				return;
			case "preview.snapshot":
			case "preview.frame":
			case "preview.cleared":
				previewPayload = envelope.payload as PreviewPayload;
				return;
		}
	}

	function moduleLabel(moduleId: string | null | undefined) {
		if (!moduleId) {
			return "No active module";
		}

		return inputModules.find((moduleDefinition) => moduleDefinition.moduleId === moduleId)?.displayName ?? moduleId;
	}

	function deviceLedCount(device: Device) {
		return device.segments.reduce((total, segment) => total + segment.length, 0);
	}

	function moduleCategoryCount(moduleDefinition: InputDefinition) {
		return new Set(moduleDefinition.channels.map((channel) => channel.category)).size;
	}

	onMount(() => {
		let disposed = false;
		let eventsSocket: WebSocket | null = null;
		let previewSocket: WebSocket | null = null;
		let reconnectEventsTimer: number | undefined;
		let reconnectPreviewTimer: number | undefined;

		const openEventsSocket = () => {
			if (disposed) {
				return;
			}

			try {
				eventsSocket = createSocket("/ws/events");
			} catch (error) {
				errorMessage = toMessage(error);
				return;
			}

			eventsSocket.addEventListener("open", () => {
				eventsConnected = true;
			});

			eventsSocket.addEventListener("message", (event) => {
				try {
					handleRuntimeEvent(JSON.parse(event.data) as RuntimeEnvelope);
				} catch (error) {
					errorMessage = toMessage(error);
				}
			});

			eventsSocket.addEventListener("close", () => {
				eventsConnected = false;

				if (!disposed) {
					reconnectEventsTimer = window.setTimeout(openEventsSocket, 2_000);
				}
			});

			eventsSocket.addEventListener("error", () => {
				eventsSocket?.close();
			});
		};

		const openPreviewSocket = () => {
			if (disposed) {
				return;
			}

			try {
				previewSocket = createSocket("/ws/preview");
			} catch (error) {
				errorMessage = toMessage(error);
				return;
			}

			previewSocket.addEventListener("open", () => {
				previewConnected = true;
			});

			previewSocket.addEventListener("message", (event) => {
				try {
					handlePreviewEvent(JSON.parse(event.data) as RuntimeEnvelope);
				} catch (error) {
					errorMessage = toMessage(error);
				}
			});

			previewSocket.addEventListener("close", () => {
				previewConnected = false;

				if (!disposed) {
					reconnectPreviewTimer = window.setTimeout(openPreviewSocket, 2_000);
				}
			});

			previewSocket.addEventListener("error", () => {
				previewSocket?.close();
			});
		};

		const pollingHandle = window.setInterval(() => {
			if (!eventsConnected) {
				void refreshDashboard();
			}
		}, 15_000);

		void refreshDashboard();
		openEventsSocket();
		openPreviewSocket();

		return () => {
			disposed = true;
			window.clearInterval(pollingHandle);

			if (reconnectEventsTimer) {
				window.clearTimeout(reconnectEventsTimer);
			}

			if (reconnectPreviewTimer) {
				window.clearTimeout(reconnectPreviewTimer);
			}

			eventsSocket?.close();
			previewSocket?.close();
		};
	});
</script>

<svelte:head>
	<title>LucaLights Control Room</title>
	<meta
		name="description"
		content="Live LucaLights runtime dashboard for input validation, preview monitoring, and effect inventory."
	/>
</svelte:head>

<div class="relative min-h-screen overflow-hidden bg-(image:--page-gradient) text-foreground">
	<div class="pointer-events-none absolute inset-0 bg-(image:--page-overlay)"></div>

	<section class="relative mx-auto flex max-w-7xl flex-col gap-6 px-4 py-6 sm:px-6 lg:px-8 lg:py-10">
		<div class="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
			<div class="max-w-3xl space-y-2">
				<h1 class="text-3xl font-semibold tracking-tight sm:text-4xl">Control Room</h1>
				<p class="max-w-2xl text-sm leading-6 text-muted-foreground sm:text-base">
					Live runtime console for game input, device topology, and preview output.
				</p>
			</div>

			<div class="flex flex-wrap items-center gap-2">
				<Badge variant={eventsConnected ? "default" : "secondary"}>
					<Cable />
					Events {eventsConnected ? "live" : "retrying"}
				</Badge>
				<Badge variant={previewConnected ? "default" : "secondary"}>
					<Activity />
					Preview {previewConnected ? "live" : "retrying"}
				</Badge>
				<Badge variant={systemStatus?.input.connected ? "default" : "outline"}>
					<Gamepad2 />
					Input {systemStatus?.input.connected ? "connected" : "idle"}
				</Badge>
				<Button variant="outline" onclick={refreshDashboard} disabled={refreshing || restarting}>
					<RefreshCw class={refreshing ? "animate-spin" : ""} />
					Refresh
				</Button>
				<Button onclick={restartEngine} disabled={refreshing || restarting}>
					<RotateCcw class={restarting ? "animate-spin" : ""} />
					Restart Engine
				</Button>
			</div>
		</div>

		{#if errorMessage}
			<div class="rounded-2xl border border-destructive/30 bg-destructive/10 px-4 py-3 text-sm text-destructive shadow-sm">
				{errorMessage}
			</div>
		{/if}

		<div class="grid gap-4 md:grid-cols-3">
			<Card class="border-surface-card-border bg-surface-card shadow-sm backdrop-blur">
				<CardHeader>
					<CardDescription class="flex items-center gap-2">
						<Cpu class="size-4" />
						Engine state
					</CardDescription>
					<CardTitle class="text-2xl">
						{systemStatus?.lighting.running ? "Running" : "Stopped"}
					</CardTitle>
					<CardAction>
						<Badge variant={systemStatus?.settings.dirty ? "secondary" : "outline"}>
							{systemStatus?.settings.dirty ? "Dirty config" : "Saved"}
						</Badge>
					</CardAction>
				</CardHeader>
				<CardContent class="space-y-2 text-sm text-muted-foreground">
					<p>Graph: {systemStatus?.settings.graphNodes ?? 0} nodes / {systemStatus?.settings.graphConnections ?? 0} connections</p>
					<p>Latest event: {latestRuntimeEvent?.type ?? "Waiting for runtime stream"}</p>
				</CardContent>
			</Card>

			<Card class="border-surface-card-border bg-surface-card shadow-sm backdrop-blur">
				<CardHeader>
					<CardDescription class="flex items-center gap-2">
						<Layers3 class="size-4" />
						Topology
					</CardDescription>
					<CardTitle class="text-2xl">
						{systemStatus?.settings.devices ?? 0} devices
					</CardTitle>
				</CardHeader>
				<CardContent class="space-y-2 text-sm text-muted-foreground">
					<p>{devices.reduce((total, device) => total + deviceLedCount(device), 0)} LEDs configured</p>
					<p>{previewPayload?.totalLedCount ?? 0} LEDs visible in preview sampling</p>
				</CardContent>
			</Card>

			<Card class="border-surface-card-border bg-surface-card shadow-sm backdrop-blur">
				<CardHeader>
					<CardDescription class="flex items-center gap-2">
						<Workflow class="size-4" />
						Node catalog
					</CardDescription>
					<CardTitle class="text-2xl">{nodeTypeCount} registered node types</CardTitle>
				</CardHeader>
				<CardContent class="space-y-2 text-sm text-muted-foreground">
					<p>{inputModules.length} input modules discovered</p>
					<p>{previewDeviceCount} preview devices currently streaming</p>
				</CardContent>
			</Card>
		</div>

			<div class="grid gap-6 xl:grid-cols-[minmax(0,1.15fr)_minmax(22rem,0.85fr)]">
				<div class="space-y-6">
					<LiveInputPreview
						snapshot={inputSnapshot}
						moduleDefinition={activeModuleDefinition}
						moduleLabel={moduleLabel(systemStatus?.input.activeModuleId)}
						connected={inputConnected}
					/>

				<Card class="border-surface-card-border bg-surface-card-alt shadow-sm backdrop-blur">
					<CardHeader>
						<CardTitle>Preview stream</CardTitle>
						<CardDescription>
							Latest sampled LED data from <span class="font-medium">{previewDeviceCount}</span> device
							{previewDeviceCount === 1 ? "" : "s"}.
						</CardDescription>
					</CardHeader>
					<CardContent class="space-y-4">
						{#if previewDevices.length > 0}
							<div class="space-y-4">
								{#each previewDevices as device}
									<div class="rounded-2xl border border-border/70 bg-background/65 p-4 shadow-sm">
										<div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
										<div>
											<p class="text-base font-semibold">{device.name}</p>
											<p class="text-sm text-muted-foreground">
													{device.ip} · {protocolLabel(device.protocol)} · {device.ledCount} LEDs
											</p>
										</div>
											<Badge variant="outline">{device.segments.length} segments</Badge>
										</div>
										<div class="mt-4 space-y-3">
											{#each device.segments as segment}
												<div class="space-y-2">
													<div class="flex items-center justify-between gap-3 text-sm">
														<div>
															<p class="font-medium">{segment.name}</p>
															<p class="text-xs text-muted-foreground">
																{segment.length} LEDs · showing {segment.colors.length}
															</p>
														</div>
														<span class="font-mono text-xs text-muted-foreground">{segment.id}</span>
													</div>
													<PreviewStrip colors={segment.colors} />
												</div>
											{/each}
										</div>
									</div>
								{/each}
							</div>
						{:else}
							<div class="rounded-2xl border border-dashed border-border/80 bg-background/50 px-4 py-8 text-center text-sm text-muted-foreground">
								Preview data will appear here as soon as the runtime starts broadcasting frames.
							</div>
						{/if}
					</CardContent>
				</Card>
			</div>

			<div class="space-y-6">
				<Card class="border-surface-card-border bg-surface-card-alt shadow-sm backdrop-blur">
					<CardHeader>
						<CardTitle>Devices</CardTitle>
						<CardDescription>
							Physical targets currently configured in the runtime settings.
						</CardDescription>
						<CardAction>
							<Button size="sm" variant="outline" href="/devices">
								<Cable class="size-3.5" />
								Manage
							</Button>
						</CardAction>
					</CardHeader>
					<CardContent class="space-y-3">
						{#if devices.length > 0}
							{#each devices as device}
								<div class="rounded-2xl border border-border/70 bg-background/65 p-4">
									<div class="flex items-start justify-between gap-3">
										<div>
											<p class="text-base font-semibold">{device.name}</p>
											<p class="text-sm text-muted-foreground">
												{device.ip} · {protocolLabel(device.protocol)}
											</p>
										</div>
										<Badge variant="outline">{deviceLedCount(device)} LEDs</Badge>
									</div>
									<div class="mt-3 flex flex-wrap gap-2">
										{#each device.segments as segment}
											<Badge variant="secondary">
												{segment.name} · {segment.length}
											</Badge>
										{/each}
									</div>
								</div>
							{/each}
						{:else}
							<p class="text-sm text-muted-foreground">No devices configured yet.</p>
						{/if}
					</CardContent>
				</Card>

				<Card class="border-surface-card-border bg-surface-card-alt shadow-sm backdrop-blur">
					<CardHeader>
						<CardTitle>Node Graph</CardTitle>
						<CardDescription>
							The unified lighting graph driving all output devices.
						</CardDescription>
					</CardHeader>
					<CardContent class="space-y-3">
						<div class="rounded-2xl border border-border/70 bg-background/65 p-4">
							<div class="flex items-start justify-between gap-3">
								<div>
									<p class="text-base font-semibold">
										{systemStatus?.settings.graphNodes ?? 0} nodes
									</p>
									<p class="text-sm text-muted-foreground">
										{systemStatus?.settings.graphConnections ?? 0} connections
									</p>
								</div>
								<Badge variant="outline">{nodeTypeCount} types available</Badge>
							</div>
							<div class="mt-3">
								<Button size="sm" href="/editor">
									<Workflow class="size-3.5" />
									Open Editor
								</Button>
							</div>
						</div>
					</CardContent>
				</Card>

				<Card class="border-surface-card-border bg-surface-card-alt shadow-sm backdrop-blur">
					<CardHeader>
						<CardTitle>Input modules</CardTitle>
						<CardDescription>
							Registered module contracts available to the node engine.
						</CardDescription>
					</CardHeader>
					<CardContent class="space-y-3">
						{#if inputModules.length > 0}
							{#each inputModules as moduleDefinition}
								<div class="rounded-2xl border border-border/70 bg-background/65 p-4">
									<div class="flex items-start justify-between gap-3">
										<div>
											<p class="text-base font-semibold">{moduleDefinition.displayName}</p>
											<p class="font-mono text-xs text-muted-foreground">{moduleDefinition.moduleId}</p>
										</div>
										{#if moduleDefinition.moduleId === systemStatus?.input.activeModuleId}
											<Badge>Live</Badge>
										{:else}
											<Badge variant="outline">Available</Badge>
										{/if}
									</div>
									<div class="mt-3 flex flex-wrap gap-2 text-xs text-muted-foreground">
										<span>{moduleDefinition.channels.length} channels</span>
										<span>·</span>
										<span>{moduleCategoryCount(moduleDefinition)} categories</span>
									</div>
								</div>
							{/each}
						{:else}
							<p class="text-sm text-muted-foreground">No input modules registered.</p>
						{/if}
					</CardContent>
				</Card>
			</div>
		</div>
	</section>
</div>
