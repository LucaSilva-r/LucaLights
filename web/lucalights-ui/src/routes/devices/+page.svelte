<script lang="ts">
	import { onMount } from 'svelte';
	import {
		Cable,
		HardDrive,
		Layers3,
		Loader2,
		Plus,
		RefreshCw,
		Save,
		Trash2
	} from '@lucide/svelte';
	import { Badge } from '$lib/components/ui/badge';
	import { Button } from '$lib/components/ui/button';
	import {
		Card,
		CardAction,
		CardContent,
		CardDescription,
		CardHeader,
		CardTitle
	} from '$lib/components/ui/card';
	import { Separator } from '$lib/components/ui/separator';
	import {
		Table,
		TableBody,
		TableCell,
		TableHead,
		TableHeader,
		TableRow
	} from '$lib/components/ui/table';
	import {
		DEVICE_PROTOCOL_OPTIONS,
		apiDelete,
		apiGet,
		apiPost,
		apiPut,
		normalizeProtocol,
		protocolLabel,
		toMessage,
		type Device,
		type Segment
	} from '$lib/lucalights';

	type DeviceForm = {
		name: string;
		ip: string;
		protocol: number;
	};

	const fieldClass =
		'h-10 w-full rounded-xl border border-border/70 bg-background/80 px-3 text-sm shadow-sm outline-none transition focus:border-ring focus:ring-4 focus:ring-ring/20';
	const segmentFieldClass =
		'h-9 w-full rounded-lg border border-border/70 bg-background/80 px-3 text-sm shadow-sm outline-none transition focus:border-ring focus:ring-4 focus:ring-ring/20 disabled:cursor-not-allowed disabled:opacity-60';

	let devices = $state<Device[]>([]);
	let selectedDeviceId = $state<string | null>(null);
	let deviceForm = $state<DeviceForm>({
		name: '',
		ip: '',
		protocol: 0
	});
	let segmentDrafts = $state<Segment[]>([]);
	let segmentGroupInputs = $state<Record<string, string>>({});
	let loading = $state(true);
	let refreshing = $state(false);
	let creatingDevice = $state(false);
	let savingDevice = $state(false);
	let deletingDevice = $state(false);
	let creatingSegment = $state(false);
	let savingSegmentId = $state<string | null>(null);
	let deletingSegmentId = $state<string | null>(null);
	let deviceDirty = $state(false);
	let errorMessage = $state('');
	let successMessage = $state('');

	let selectedDevice = $derived(devices.find((device) => device.id === selectedDeviceId) ?? null);
	let totalLedCount = $derived(
		devices.reduce(
			(total, device) =>
				total + device.segments.reduce((segmentTotal, segment) => segmentTotal + segment.length, 0),
			0
		)
	);
	let totalSegmentCount = $derived(
		devices.reduce((total, device) => total + device.segments.length, 0)
	);
	let selectedLedCount = $derived(
		segmentDrafts.reduce((total, segment) => total + segment.length, 0)
	);
	let segmentActionsDisabled = $derived(deviceDirty || savingDevice || deletingDevice);

	function cloneSegment(segment: Segment): Segment {
		return {
			id: segment.id,
			name: segment.name,
			groupIds: [...segment.groupIds],
			length: segment.length
		};
	}

	function emptyDeviceForm(): DeviceForm {
		return {
			name: '',
			ip: '',
			protocol: 0
		};
	}

	function formatGroupIds(groupIds: number[]) {
		return groupIds.join(', ');
	}

	function parseGroupIds(rawValue: string) {
		const values: number[] = [];
		const seen = new Set<number>();

		for (const chunk of rawValue.split(',')) {
			const trimmed = chunk.trim();
			if (!trimmed) {
				continue;
			}

			const parsed = Number(trimmed);
			if (!Number.isInteger(parsed) || seen.has(parsed)) {
				continue;
			}

			values.push(parsed);
			seen.add(parsed);
		}

		return values;
	}

	function beginEditing(device: Device | null) {
		selectedDeviceId = device?.id ?? null;
		successMessage = '';

		if (!device) {
			deviceForm = emptyDeviceForm();
			segmentDrafts = [];
			segmentGroupInputs = {};
			deviceDirty = false;
			return;
		}

		deviceForm = {
			name: device.name,
			ip: device.ip,
			protocol: normalizeProtocol(device.protocol)
		};
		segmentDrafts = device.segments.map(cloneSegment);
		segmentGroupInputs = Object.fromEntries(
			device.segments.map((segment) => [segment.id, formatGroupIds(segment.groupIds)])
		);
		deviceDirty = false;
	}

	function selectDevice(deviceId: string) {
		if (deviceId === selectedDeviceId) {
			return;
		}

		if (deviceDirty && !window.confirm('Discard unsaved device detail changes?')) {
			return;
		}

		beginEditing(devices.find((device) => device.id === deviceId) ?? null);
		errorMessage = '';
	}

	function replaceDeviceInState(updatedDevice: Device) {
		const nextDevices = devices.slice();
		const index = nextDevices.findIndex((device) => device.id === updatedDevice.id);

		if (index >= 0) {
			nextDevices[index] = updatedDevice;
		} else {
			nextDevices.push(updatedDevice);
		}

		devices = nextDevices;

		if (selectedDeviceId === updatedDevice.id) {
			beginEditing(updatedDevice);
		}
	}

	async function loadDevices(preferredDeviceId: string | null = selectedDeviceId) {
		refreshing = true;

		try {
			const deviceList = await apiGet<Device[]>('/api/devices');
			devices = deviceList;

			if (deviceList.length === 0) {
				beginEditing(null);
			} else {
				beginEditing(
					deviceList.find((device) => device.id === preferredDeviceId) ?? deviceList[0]
				);
			}

			errorMessage = '';
		} catch (error) {
			errorMessage = toMessage(error);
		} finally {
			refreshing = false;
			loading = false;
		}
	}

	function markDeviceDirty() {
		deviceDirty = true;
		successMessage = '';
	}

	function resetDeviceForm() {
		beginEditing(selectedDevice);
		errorMessage = '';
	}

	async function createDevice() {
		creatingDevice = true;

		try {
			const createdDevice = await apiPost<Device>('/api/devices', {
				id: '',
				name: `Device ${devices.length + 1}`,
				ip: '192.168.1.50',
				protocol: 0,
				segments: [
					{
						id: '',
						name: 'Main',
						groupIds: [],
						length: 60
					}
				]
			});

			devices = [...devices, createdDevice];
			beginEditing(createdDevice);
			errorMessage = '';
			successMessage = `Created ${createdDevice.name}.`;
		} catch (error) {
			errorMessage = toMessage(error);
		} finally {
			creatingDevice = false;
		}
	}

	async function saveDevice() {
		const currentSelectedDevice = selectedDevice;
		if (!currentSelectedDevice || !selectedDeviceId) {
			return;
		}

		savingDevice = true;

		try {
			const updatedDevice = await apiPut<Device>(
				`/api/devices/${encodeURIComponent(selectedDeviceId)}`,
				{
					...currentSelectedDevice,
					name: deviceForm.name.trim() || 'New Device',
					ip: deviceForm.ip.trim() || '192.168.1.1',
					protocol: normalizeProtocol(deviceForm.protocol),
					segments: currentSelectedDevice.segments.map(cloneSegment)
				}
			);

			replaceDeviceInState(updatedDevice);
			errorMessage = '';
			successMessage = `Saved ${updatedDevice.name}.`;
		} catch (error) {
			errorMessage = toMessage(error);
		} finally {
			savingDevice = false;
		}
	}

	async function deleteSelectedDevice() {
		const currentSelectedDevice = selectedDevice;
		if (!currentSelectedDevice) {
			return;
		}

		if (!window.confirm(`Delete ${currentSelectedDevice.name}? This also removes its segments.`)) {
			return;
		}

		deletingDevice = true;

		try {
			await apiDelete(`/api/devices/${encodeURIComponent(currentSelectedDevice.id)}`);

			const deletedIndex = devices.findIndex((device) => device.id === currentSelectedDevice.id);
			const nextDevices = devices.filter((device) => device.id !== currentSelectedDevice.id);
			devices = nextDevices;

			const fallbackDevice =
				nextDevices[Math.min(deletedIndex, Math.max(0, nextDevices.length - 1))] ?? null;
			beginEditing(fallbackDevice);

			errorMessage = '';
			successMessage = `Deleted ${currentSelectedDevice.name}.`;
		} catch (error) {
			errorMessage = toMessage(error);
		} finally {
			deletingDevice = false;
		}
	}

	function updateSegmentName(segmentId: string, name: string) {
		segmentDrafts = segmentDrafts.map((segment) =>
			segment.id === segmentId ? { ...segment, name } : segment
		);
		successMessage = '';
	}

	function updateSegmentLength(segmentId: string, length: number) {
		segmentDrafts = segmentDrafts.map((segment) =>
			segment.id === segmentId
				? { ...segment, length: Number.isFinite(length) ? Math.max(0, length) : 0 }
				: segment
		);
		successMessage = '';
	}

	function updateSegmentGroups(segmentId: string, value: string) {
		segmentGroupInputs = {
			...segmentGroupInputs,
			[segmentId]: value
		};
		successMessage = '';
	}

	async function addSegment() {
		const currentSelectedDevice = selectedDevice;
		if (!currentSelectedDevice || !selectedDeviceId || segmentActionsDisabled) {
			return;
		}

		creatingSegment = true;

		try {
			const createdSegment = await apiPost<Segment>(
				`/api/devices/${encodeURIComponent(selectedDeviceId)}/segments`,
				{
					id: '',
					name: `Segment ${segmentDrafts.length + 1}`,
					groupIds: [],
					length: 60
				}
			);

			replaceDeviceInState({
				...currentSelectedDevice,
				segments: [...currentSelectedDevice.segments, createdSegment]
			});

			errorMessage = '';
			successMessage = `Added ${createdSegment.name}.`;
		} catch (error) {
			errorMessage = toMessage(error);
		} finally {
			creatingSegment = false;
		}
	}

	async function saveSegment(segmentId: string) {
		const currentSelectedDevice = selectedDevice;
		const segment = segmentDrafts.find((entry) => entry.id === segmentId);

		if (!currentSelectedDevice || !selectedDeviceId || !segment) {
			return;
		}

		savingSegmentId = segmentId;

		try {
			const updatedSegment = await apiPut<Segment>(
				`/api/devices/${encodeURIComponent(selectedDeviceId)}/segments/${encodeURIComponent(segmentId)}`,
				{
					...segment,
					name: segment.name.trim() || 'New Segment',
					length: Math.max(0, Number(segment.length) || 0),
					groupIds: parseGroupIds(segmentGroupInputs[segmentId] ?? '')
				}
			);

			replaceDeviceInState({
				...currentSelectedDevice,
				segments: currentSelectedDevice.segments.map((entry) =>
					entry.id === segmentId ? updatedSegment : entry
				)
			});

			errorMessage = '';
			successMessage = `Saved ${updatedSegment.name}.`;
		} catch (error) {
			errorMessage = toMessage(error);
		} finally {
			savingSegmentId = null;
		}
	}

	async function deleteSegment(segmentId: string) {
		const currentSelectedDevice = selectedDevice;
		const segment = segmentDrafts.find((entry) => entry.id === segmentId);

		if (!currentSelectedDevice || !selectedDeviceId || !segment) {
			return;
		}

		if (!window.confirm(`Delete segment ${segment.name}?`)) {
			return;
		}

		deletingSegmentId = segmentId;

		try {
			await apiDelete(
				`/api/devices/${encodeURIComponent(selectedDeviceId)}/segments/${encodeURIComponent(segmentId)}`
			);

			replaceDeviceInState({
				...currentSelectedDevice,
				segments: currentSelectedDevice.segments.filter((entry) => entry.id !== segmentId)
			});

			errorMessage = '';
			successMessage = `Deleted ${segment.name}.`;
		} catch (error) {
			errorMessage = toMessage(error);
		} finally {
			deletingSegmentId = null;
		}
	}

	onMount(() => {
		void loadDevices();
	});
</script>

<svelte:head>
	<title>Devices - LucaLights</title>
	<meta
		name="description"
		content="Configure LucaLights output devices, protocols, and segment layouts."
	/>
</svelte:head>

<div class="relative min-h-screen overflow-hidden bg-[linear-gradient(180deg,#f8f5f0_0%,#f2ede5_35%,#ece6de_100%)] text-foreground">
	<div class="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_top_left,rgba(50,35,20,0.14),transparent_34%),radial-gradient(circle_at_top_right,rgba(150,117,83,0.18),transparent_28%)]"></div>

	<section class="relative mx-auto flex max-w-7xl flex-col gap-6 px-4 py-6 sm:px-6 lg:px-8 lg:py-10">
		<div class="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
			<div class="max-w-3xl space-y-2">
				<h1 class="text-3xl font-semibold tracking-tight sm:text-4xl">Device Manager</h1>
				<p class="max-w-2xl text-sm leading-6 text-muted-foreground sm:text-base">
					Define output targets, choose the transport protocol, and shape the segment layout the
					unified graph will render into.
				</p>
			</div>

			<div class="flex flex-wrap items-center gap-2">
				<Button variant="outline" onclick={() => loadDevices()} disabled={refreshing || loading}>
					<RefreshCw class={refreshing ? 'animate-spin' : ''} />
					Refresh
				</Button>
				<Button onclick={createDevice} disabled={creatingDevice}>
					{#if creatingDevice}
						<Loader2 class="animate-spin" />
					{:else}
						<Plus />
					{/if}
					New Device
				</Button>
			</div>
		</div>

		{#if errorMessage}
			<div class="rounded-2xl border border-destructive/30 bg-destructive/10 px-4 py-3 text-sm text-destructive shadow-sm">
				{errorMessage}
			</div>
		{/if}

		{#if successMessage}
			<div class="rounded-2xl border border-border/80 bg-white/80 px-4 py-3 text-sm text-foreground shadow-sm backdrop-blur">
				{successMessage}
			</div>
		{/if}

		<div class="grid gap-4 md:grid-cols-3">
			<Card class="border-white/60 bg-white/80 shadow-sm backdrop-blur">
				<CardHeader>
					<CardDescription class="flex items-center gap-2">
						<HardDrive class="size-4" />
						Devices
					</CardDescription>
					<CardTitle class="text-2xl">{devices.length}</CardTitle>
				</CardHeader>
				<CardContent class="text-sm text-muted-foreground">
					{devices.length === 1
						? 'Single output target configured.'
						: 'Configured output targets ready for routing.'}
				</CardContent>
			</Card>

			<Card class="border-white/60 bg-white/80 shadow-sm backdrop-blur">
				<CardHeader>
					<CardDescription class="flex items-center gap-2">
						<Cable class="size-4" />
						LED footprint
					</CardDescription>
					<CardTitle class="text-2xl">{totalLedCount}</CardTitle>
				</CardHeader>
				<CardContent class="text-sm text-muted-foreground">
					Total LED count across every configured device.
				</CardContent>
			</Card>

			<Card class="border-white/60 bg-white/80 shadow-sm backdrop-blur">
				<CardHeader>
					<CardDescription class="flex items-center gap-2">
						<Layers3 class="size-4" />
						Segments
					</CardDescription>
					<CardTitle class="text-2xl">{totalSegmentCount}</CardTitle>
				</CardHeader>
				<CardContent class="text-sm text-muted-foreground">
					Reusable output slices exposed to the graph renderer.
				</CardContent>
			</Card>
		</div>

		<div class="grid gap-6 xl:grid-cols-[18rem_minmax(0,1fr)]">
			<Card class="border-white/60 bg-white/82 shadow-sm backdrop-blur">
				<CardHeader>
					<CardTitle>Configured devices</CardTitle>
					<CardDescription>
						Select a target to edit its connection settings and segment layout.
					</CardDescription>
					<CardAction>
						<Badge variant="outline">{devices.length}</Badge>
					</CardAction>
				</CardHeader>
				<CardContent class="space-y-3">
					{#if loading}
						<div class="flex items-center gap-2 rounded-2xl border border-border/70 bg-background/65 px-4 py-3 text-sm text-muted-foreground">
							<Loader2 class="size-4 animate-spin" />
							Loading devices...
						</div>
					{:else if devices.length > 0}
						{#each devices as device}
							<button
								type="button"
								class={`w-full rounded-2xl border p-4 text-left transition ${
									device.id === selectedDeviceId
										? 'border-primary/35 bg-primary/8 shadow-sm'
										: 'border-border/70 bg-background/65 hover:border-border hover:bg-background/80'
								}`}
								onclick={() => selectDevice(device.id)}
							>
								<div class="flex items-start justify-between gap-3">
									<div>
										<p class="text-sm font-semibold">{device.name}</p>
										<p class="text-xs text-muted-foreground">{device.ip}</p>
									</div>
									<Badge variant={device.id === selectedDeviceId ? 'default' : 'outline'}>
										{device.segments.length}
									</Badge>
								</div>
								<p class="mt-3 text-xs uppercase tracking-[0.18em] text-muted-foreground">
									{protocolLabel(device.protocol)} · {device.segments.reduce((total, segment) => total + segment.length, 0)} leds
								</p>
							</button>
						{/each}
					{:else}
						<div class="rounded-2xl border border-dashed border-border/80 bg-background/50 px-4 py-8 text-center text-sm text-muted-foreground">
							No devices configured yet.
						</div>
					{/if}
				</CardContent>
			</Card>

			{#if selectedDevice}
				<div class="space-y-6">
					<Card class="border-white/60 bg-white/82 shadow-sm backdrop-blur">
						<CardHeader class="space-y-3">
							<div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
								<div class="space-y-1">
									<CardTitle>{selectedDevice.name}</CardTitle>
									<CardDescription>
										Adjust the transport settings before the graph routes output into this device.
									</CardDescription>
								</div>
								<div class="flex flex-wrap gap-2">
									<Badge variant="outline">{selectedLedCount} LEDs</Badge>
									<Badge variant="secondary">{segmentDrafts.length} segments</Badge>
								</div>
							</div>
							<div class="flex flex-wrap gap-x-4 gap-y-1 text-xs uppercase tracking-[0.2em] text-muted-foreground">
								<span>ID {selectedDevice.id}</span>
								<span>{protocolLabel(deviceForm.protocol)}</span>
							</div>
						</CardHeader>

						<CardContent class="space-y-5">
							<div class="grid gap-4 lg:grid-cols-[minmax(0,1.2fr)_minmax(0,1fr)_13rem]">
								<label class="space-y-2 text-sm font-medium">
									<span>Name</span>
									<input
										class={fieldClass}
										bind:value={deviceForm.name}
										oninput={markDeviceDirty}
										placeholder="Dance Floor Left"
									/>
								</label>

								<label class="space-y-2 text-sm font-medium">
									<span>IP address</span>
									<input
										class={fieldClass}
										bind:value={deviceForm.ip}
										oninput={markDeviceDirty}
										placeholder="192.168.1.50"
									/>
								</label>

								<label class="space-y-2 text-sm font-medium">
									<span>Protocol</span>
									<select
										class={fieldClass}
										bind:value={deviceForm.protocol}
										onchange={markDeviceDirty}
									>
										{#each DEVICE_PROTOCOL_OPTIONS as option}
											<option value={option.value}>{option.label}</option>
										{/each}
									</select>
								</label>
							</div>

							<div class="flex flex-wrap items-center gap-2">
								<Button onclick={saveDevice} disabled={!deviceDirty || savingDevice || deletingDevice}>
									{#if savingDevice}
										<Loader2 class="animate-spin" />
									{:else}
										<Save />
									{/if}
									Save Device
								</Button>
								<Button
									variant="outline"
									onclick={resetDeviceForm}
									disabled={!deviceDirty || savingDevice || deletingDevice}
								>
									Reset
								</Button>
								<Button
									variant="destructive"
									onclick={deleteSelectedDevice}
									disabled={savingDevice || deletingDevice}
								>
									{#if deletingDevice}
										<Loader2 class="animate-spin" />
									{:else}
										<Trash2 />
									{/if}
									Delete Device
								</Button>
							</div>
						</CardContent>
					</Card>

					<Card class="border-white/60 bg-white/82 shadow-sm backdrop-blur">
						<CardHeader class="space-y-3">
							<div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
								<div class="space-y-1">
									<CardTitle>Segments</CardTitle>
									<CardDescription>
										These slices are the routing targets the unified graph can write into.
									</CardDescription>
								</div>
								<Button
									size="sm"
									variant="outline"
									onclick={addSegment}
									disabled={creatingSegment || segmentActionsDisabled}
								>
									{#if creatingSegment}
										<Loader2 class="animate-spin" />
									{:else}
										<Plus />
									{/if}
									Add Segment
								</Button>
							</div>

							{#if deviceDirty}
								<div class="rounded-xl border border-border/70 bg-background/70 px-3 py-2 text-sm text-muted-foreground">
									Save or reset the device details before changing segments.
								</div>
							{/if}
						</CardHeader>

						<CardContent class="space-y-4">
							{#if segmentDrafts.length > 0}
								<div class="overflow-hidden rounded-2xl border border-border/70 bg-background/70">
									<Table>
										<TableHeader>
											<TableRow>
												<TableHead>Name</TableHead>
												<TableHead class="w-28">Length</TableHead>
												<TableHead>Group IDs</TableHead>
												<TableHead class="w-44 text-right">Actions</TableHead>
											</TableRow>
										</TableHeader>
										<TableBody>
											{#each segmentDrafts as segment}
												<TableRow>
													<TableCell class="align-top">
														<input
															class={segmentFieldClass}
															value={segment.name}
															oninput={(event) =>
																updateSegmentName(
																	segment.id,
																	(event.currentTarget as HTMLInputElement).value
																)}
															disabled={segmentActionsDisabled}
															placeholder="Marquee Left"
														/>
													</TableCell>
													<TableCell class="align-top">
														<input
															class={segmentFieldClass}
															type="number"
															min="0"
															value={segment.length}
															oninput={(event) =>
																updateSegmentLength(
																	segment.id,
																	Number((event.currentTarget as HTMLInputElement).value)
																)}
															disabled={segmentActionsDisabled}
														/>
													</TableCell>
													<TableCell class="align-top">
														<input
															class={segmentFieldClass}
															value={segmentGroupInputs[segment.id] ?? ''}
															oninput={(event) =>
																updateSegmentGroups(
																	segment.id,
																	(event.currentTarget as HTMLInputElement).value
																)}
															disabled={segmentActionsDisabled}
															placeholder="0, 1, 2"
														/>
													</TableCell>
													<TableCell class="align-top">
														<div class="flex justify-end gap-2">
															<Button
																size="xs"
																variant="outline"
																onclick={() => saveSegment(segment.id)}
																disabled={
																	segmentActionsDisabled ||
																	savingSegmentId === segment.id ||
																	deletingSegmentId === segment.id
																}
															>
																{#if savingSegmentId === segment.id}
																	<Loader2 class="animate-spin" />
																{:else}
																	<Save />
																{/if}
																Save
															</Button>
															<Button
																size="xs"
																variant="destructive"
																onclick={() => deleteSegment(segment.id)}
																disabled={
																	segmentActionsDisabled ||
																	deletingSegmentId === segment.id ||
																	savingSegmentId === segment.id
																}
															>
																{#if deletingSegmentId === segment.id}
																	<Loader2 class="animate-spin" />
																{:else}
																	<Trash2 />
																{/if}
																Delete
															</Button>
														</div>
													</TableCell>
												</TableRow>
											{/each}
										</TableBody>
									</Table>
								</div>
							{:else}
								<div class="rounded-2xl border border-dashed border-border/80 bg-background/50 px-4 py-8 text-center text-sm text-muted-foreground">
									This device does not have any segments yet.
								</div>
							{/if}

							<Separator />

							<p class="text-sm text-muted-foreground">
								Group IDs are comma-separated integers. Save a row after editing it to persist the
								change.
							</p>
						</CardContent>
					</Card>
				</div>
			{:else}
				<Card class="border-white/60 bg-white/82 shadow-sm backdrop-blur">
					<CardHeader>
						<CardTitle>No device selected</CardTitle>
						<CardDescription>
							Create your first output target to start routing graph output into real hardware.
						</CardDescription>
					</CardHeader>
					<CardContent>
						<Button onclick={createDevice} disabled={creatingDevice}>
							{#if creatingDevice}
								<Loader2 class="animate-spin" />
							{:else}
								<Plus />
							{/if}
							Create Device
						</Button>
					</CardContent>
				</Card>
			{/if}
		</div>
	</section>
</div>
