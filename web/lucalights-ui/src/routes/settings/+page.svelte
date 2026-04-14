<script lang="ts">
	import { onMount } from 'svelte';
	import {
		Check,
		Cpu,
		Gamepad2,
		HardDrive,
		Loader2,
		RefreshCw,
		Save,
		Settings as SettingsIcon,
		Workflow
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
		apiGet,
		apiPut,
		toMessage,
		type InputDefinition
	} from '$lib/lucalights';

	const fieldClass =
		'h-10 w-full rounded-xl border border-border/70 bg-background/80 px-3 text-sm shadow-sm outline-none transition focus:border-ring focus:ring-4 focus:ring-ring/20';

	let settings = $state<any>(null);
	let settingsPath = $state<string>('');
	let inputModules = $state<InputDefinition[]>([]);
	let loading = $state(true);
	let saving = $state(false);
	let errorMessage = $state('');
	let successMessage = $state('');

	async function loadSettings() {
		loading = true;
		errorMessage = '';

		try {
			const [settingsResponse, modules] = await Promise.all([
				apiGet<any>('/api/settings'),
				apiGet<InputDefinition[]>('/api/input-modules')
			]);

			settings = settingsResponse.settings;
			settingsPath = settingsResponse.settingsPath;
			inputModules = modules;
		} catch (error) {
			errorMessage = toMessage(error);
		} finally {
			loading = false;
		}
	}

	async function saveSettings() {
		saving = true;
		errorMessage = '';
		successMessage = '';

		try {
			const response = await apiPut<any>('/api/settings', settings);
			settings = response.settings;
			settingsPath = response.settingsPath;
			successMessage = 'Settings saved successfully.';
		} catch (error) {
			errorMessage = toMessage(error);
		} finally {
			saving = false;
		}
	}

	onMount(loadSettings);
</script>

<svelte:head>
	<title>LucaLights Settings</title>
</svelte:head>

<div class="relative min-h-screen overflow-hidden bg-(image:--page-gradient) text-foreground">
	<div class="pointer-events-none absolute inset-0 bg-(image:--page-overlay)"></div>

	<section class="relative mx-auto flex max-w-5xl flex-col gap-6 px-4 py-6 sm:px-6 lg:px-8 lg:py-10">
		<div class="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
			<div class="max-w-3xl space-y-2">
				<h1 class="text-3xl font-semibold tracking-tight sm:text-4xl">Settings</h1>
				<p class="max-w-2xl text-sm leading-6 text-muted-foreground sm:text-base">
					Configure engine-wide behavior and active game integration modules.
				</p>
			</div>

			<div class="flex flex-wrap items-center gap-2">
				<Button variant="outline" onclick={loadSettings} disabled={loading || saving}>
					<RefreshCw class={loading ? 'animate-spin' : ''} />
					Refresh
				</Button>
				<Button onclick={saveSettings} disabled={loading || saving}>
					{#if saving}
						<Loader2 class="animate-spin" />
						Saving...
					{:else}
						<Save />
						Save Changes
					{/if}
				</Button>
			</div>
		</div>

		{#if errorMessage}
			<div class="rounded-2xl border border-destructive/30 bg-destructive/10 px-4 py-3 text-sm text-destructive shadow-sm">
				{errorMessage}
			</div>
		{/if}

		{#if successMessage}
			<div class="rounded-2xl border border-emerald-500/30 bg-emerald-500/10 px-4 py-3 text-sm text-emerald-600 dark:text-emerald-400 shadow-sm">
				{successMessage}
			</div>
		{/if}

		{#if loading && !settings}
			<div class="flex h-64 items-center justify-center">
				<Loader2 class="size-8 animate-spin text-muted-foreground" />
			</div>
		{:else if settings}
			<div class="grid gap-6">
				<Card class="border-surface-card-border bg-surface-card shadow-sm backdrop-blur">
					<CardHeader>
						<CardDescription class="flex items-center gap-2">
							<Gamepad2 class="size-4" />
							Input Engine
						</CardDescription>
						<CardTitle>Active Input Module</CardTitle>
					</CardHeader>
					<CardContent class="space-y-6">
						<div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
							{#each inputModules as module}
								<button
									type="button"
									class={`flex flex-col items-start gap-3 rounded-2xl border p-4 text-left transition ${
										settings.activeInputModuleId === module.moduleId
											? 'border-primary bg-primary/5 ring-1 ring-primary'
											: 'border-border/70 bg-background/50 hover:border-primary/40 hover:bg-background/80'
									}`}
									onclick={() => settings.activeInputModuleId = module.moduleId}
								>
									<div class="flex w-full items-center justify-between gap-2">
										<p class="font-semibold">{module.displayName}</p>
										{#if settings.activeInputModuleId === module.moduleId}
											<div class="flex size-5 items-center justify-center rounded-full bg-primary text-primary-foreground">
												<Check class="size-3" />
											</div>
										{/if}
									</div>
									<p class="font-mono text-[10px] text-muted-foreground uppercase tracking-widest">{module.moduleId}</p>
								</button>
							{/each}
						</div>

						<Separator />

						{#if settings.activeInputModuleId === 'osu'}
							<div class="space-y-4">
								<h3 class="text-sm font-semibold tracking-wide uppercase text-muted-foreground">osu! Module Settings</h3>
								<div class="grid gap-4 sm:grid-cols-2">
									<label class="space-y-1.5">
										<span class="text-xs font-medium pl-1">tosu WebSocket URL</span>
										<input
											class={fieldClass}
											type="text"
											bind:value={settings.inputModuleSettings.osu.tosuUrl}
											placeholder="ws://127.0.0.1:24050"
										/>
									</label>
									<label class="flex items-center gap-3 pt-6 cursor-pointer group">
										<input
											class="size-4 rounded border-border text-primary focus:ring-primary/20"
											type="checkbox"
											bind:checked={settings.inputModuleSettings.osu.autoManage}
										/>
										<div class="space-y-0.5">
											<span class="text-sm font-medium">Auto-manage tosu process</span>
											<p class="text-xs text-muted-foreground">Automatically download and launch tosu if not running.</p>
										</div>
									</label>
								</div>
							</div>
						{:else if settings.activeInputModuleId === 'itgmania'}
							<div class="space-y-4">
								<h3 class="text-sm font-semibold tracking-wide uppercase text-muted-foreground">ITGMania Module Settings</h3>
								<div class="grid gap-4">
									<label class="space-y-1.5">
										<span class="text-xs font-medium pl-1">SextetStream Pipe Path</span>
										<input
											class={fieldClass}
											type="text"
											bind:value={settings.inputModuleSettings.itgmania.pipeName}
											placeholder="Path to .out pipe or pipe name"
										/>
									</label>
								</div>
							</div>
						{/if}
					</CardContent>
				</Card>

				<Card class="border-surface-card-border bg-surface-card shadow-sm backdrop-blur">
					<CardHeader>
						<CardDescription class="flex items-center gap-2">
							<Cpu class="size-4" />
							Persistence
						</CardDescription>
						<CardTitle>Settings Location</CardTitle>
					</CardHeader>
					<CardContent>
						<div class="rounded-xl border border-border/70 bg-background/50 p-4">
							<div class="flex items-start gap-3">
								<HardDrive class="size-5 text-muted-foreground mt-0.5" />
								<div class="space-y-1">
									<p class="text-sm font-medium">Current Configuration File</p>
									<code class="text-xs text-muted-foreground break-all bg-muted/50 px-1.5 py-0.5 rounded">
										{settingsPath || 'Loading path...'}
									</code>
								</div>
							</div>
						</div>
					</CardContent>
				</Card>
			</div>
		{/if}
	</section>
</div>
