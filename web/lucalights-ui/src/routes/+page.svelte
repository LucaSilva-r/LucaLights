<script lang="ts">
	import {
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
		apiPut,
		createSocket,
		protocolLabel,
		toMessage,
		type ColorValue,
		type Device,
		type InputDefinition,
		type InputSimulationState,
		type InputSnapshot,
		type NodeTypesResponse,
		type RuntimeEnvelope,
		type SystemStatus
	} from "$lib/lucalights";

	let systemStatus = $state<SystemStatus | null>(null);
	let inputSnapshot = $state<InputSnapshot | null>(null);
	let inputSimulation = $state<InputSimulationState | null>(null);
	let simulationModuleId = $state("");
	let inputModules = $state<InputDefinition[]>([]);
	let devices = $state<Device[]>([]);
	let nodeTypeCount = $state(0);
	let latestRuntimeEvent = $state<RuntimeEnvelope | null>(null);
	let eventsConnected = $state(false);
	let refreshing = $state(false);
	let restarting = $state(false);
	let errorMessage = $state("");

	let activeModuleDefinition = $derived(
		inputModules.find((moduleDefinition) => moduleDefinition.moduleId === (inputSimulation?.enabled ? inputSimulation.moduleId : systemStatus?.input.activeModuleId))
	);
	let inputConnected = $derived(inputSnapshot?.isConnected ?? systemStatus?.input.connected ?? false);

	async function refreshDashboard() {
		refreshing = true;

		try {
			const [status, snapshot, simulation, moduleDefinitions, deviceList, nodeTypes] =
				await Promise.all([
					apiGet<SystemStatus>("/api/system/status"),
					apiGet<InputSnapshot>("/api/input-state"),
					apiGet<InputSimulationState>("/api/input-simulation"),
					apiGet<InputDefinition[]>("/api/input-modules"),
					apiGet<Device[]>("/api/devices"),
					apiGet<NodeTypesResponse>("/api/node-types")
				]);

			systemStatus = status;
			inputSnapshot = snapshot;
			inputSimulation = simulation;
			inputModules = moduleDefinitions;
			simulationModuleId = simulation.moduleId || status.input.activeModuleId || moduleDefinitions[0]?.moduleId || "";
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

	async function setInputSimulation(enabled: boolean) {
		const moduleId = simulationModuleId || inputModules[0]?.moduleId;
		if (!moduleId) {
			return;
		}

		try {
			const result = await apiPut<{ simulation: InputSimulationState; snapshot: InputSnapshot | null }>("/api/input-simulation", {
				moduleId,
				enabled
			});
			inputSimulation = result.simulation;
			if (result.snapshot) {
				applySnapshot(result.snapshot);
			}
			if (systemStatus) {
				systemStatus = {
					...systemStatus,
					input: {
						...systemStatus.input,
						activeModuleId: enabled ? moduleId : systemStatus.input.activeModuleId,
						connected: result.snapshot?.isConnected ?? systemStatus.input.connected,
						active: result.snapshot?.isActive ?? systemStatus.input.active
					}
				};
			}
			errorMessage = "";
		} catch (error) {
			errorMessage = toMessage(error);
		}
	}

	async function setSimulationBool(key: string, value: boolean) {
		await applySimulationSnapshot(
			apiPut<InputSnapshot>(`/api/input-simulation/bool/${encodeURIComponent(key)}`, { value })
		);
	}

	async function triggerSimulationPulse(key: string) {
		await applySimulationSnapshot(
			apiPost<InputSnapshot>(`/api/input-simulation/pulse/${encodeURIComponent(key)}`)
		);
	}

	async function setSimulationFloat(key: string, value: number) {
		await applySimulationSnapshot(
			apiPut<InputSnapshot>(`/api/input-simulation/float/${encodeURIComponent(key)}`, { value })
		);
	}

	async function setSimulationColor(key: string, value: ColorValue) {
		await applySimulationSnapshot(
			apiPut<InputSnapshot>(`/api/input-simulation/color/${encodeURIComponent(key)}`, value)
		);
	}

	async function applySimulationSnapshot(request: Promise<InputSnapshot>) {
		try {
			applySnapshot(await request);
			errorMessage = "";
		} catch (error) {
			errorMessage = toMessage(error);
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
		let reconnectEventsTimer: number | undefined;

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

		const pollingHandle = window.setInterval(() => {
			if (!eventsConnected) {
				void refreshDashboard();
			}
		}, 15_000);

		void refreshDashboard();
		openEventsSocket();

		return () => {
			disposed = true;
			window.clearInterval(pollingHandle);

			if (reconnectEventsTimer) {
				window.clearTimeout(reconnectEventsTimer);
			}

			eventsSocket?.close();
		};
	});
</script>

<svelte:head>
	<title>LucaLights Control Room</title>
	<meta
		name="description"
		content="Live LucaLights runtime dashboard for input validation, device topology, and effect inventory."
	/>
</svelte:head>

<div class="relative min-h-screen overflow-hidden bg-(image:--page-gradient) text-foreground">
	<div class="pointer-events-none absolute inset-0 bg-(image:--page-overlay)"></div>

	<section class="relative mx-auto flex max-w-7xl flex-col gap-6 px-4 py-6 sm:px-6 lg:px-8 lg:py-10">
		<div class="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
			<div class="max-w-3xl space-y-2">
				<h1 class="text-3xl font-semibold tracking-tight sm:text-4xl">Control Room</h1>
				<p class="max-w-2xl text-sm leading-6 text-muted-foreground sm:text-base">
					Live runtime console for game input, device topology, and effect inventory.
				</p>
			</div>

			<div class="flex flex-wrap items-center gap-2">
				<Badge variant={eventsConnected ? "default" : "secondary"}>
					<Cable />
					Events {eventsConnected ? "live" : "retrying"}
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
					<p>{devices.reduce((total, device) => total + device.segments.length, 0)} segments configured</p>
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
					<p>{devices.length} output devices configured</p>
				</CardContent>
			</Card>
		</div>

		<div class="grid gap-6 xl:grid-cols-[minmax(0,1.15fr)_minmax(22rem,0.85fr)]">
			<div class="space-y-6">
				<LiveInputPreview
					snapshot={inputSnapshot}
					moduleDefinition={activeModuleDefinition}
					moduleLabel={moduleLabel(inputSimulation?.enabled ? inputSimulation.moduleId : systemStatus?.input.activeModuleId)}
					connected={inputConnected}
					simulationEnabled={inputSimulation?.enabled ?? false}
					onBoolChange={setSimulationBool}
					onPulseTrigger={triggerSimulationPulse}
					onFloatChange={setSimulationFloat}
					onColorChange={setSimulationColor}
				/>
			</div>

			<div class="space-y-6">
				<Card class="border-surface-card-border bg-surface-card-alt shadow-sm backdrop-blur">
					<CardHeader>
						<CardTitle>Input Simulation</CardTitle>
						<CardDescription>
							Publish a fake module snapshot through the normal graph input pipeline.
						</CardDescription>
						<CardAction>
							<Badge variant={inputSimulation?.enabled ? "default" : "outline"}>
								{inputSimulation?.enabled ? "Active" : "Off"}
							</Badge>
						</CardAction>
					</CardHeader>
					<CardContent class="space-y-3">
						<select
							class="h-10 w-full rounded-xl border border-border/70 bg-background/80 px-3 text-sm outline-none focus:border-ring"
							value={simulationModuleId}
							disabled={inputSimulation?.enabled}
							onchange={(event) => simulationModuleId = (event.currentTarget as HTMLSelectElement).value}
						>
							{#each inputModules as moduleDefinition}
								<option value={moduleDefinition.moduleId}>{moduleDefinition.displayName}</option>
							{/each}
						</select>
						<div class="flex flex-wrap gap-2">
							{#if inputSimulation?.enabled}
								<Button variant="outline" onclick={() => setInputSimulation(false)}>Stop Simulation</Button>
							{:else}
								<Button onclick={() => setInputSimulation(true)} disabled={!simulationModuleId}>Simulate Module</Button>
							{/if}
						</div>
					</CardContent>
				</Card>

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
