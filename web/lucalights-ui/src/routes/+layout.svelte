<script lang="ts">
	import './layout.css';
	import favicon from '$lib/assets/favicon.ico';
	import { page } from '$app/state';
	import { headerActions } from '$lib/header-actions.svelte';
	import { Button } from '$lib/components/ui/button';
	import { Separator } from '$lib/components/ui/separator';
	import { Cable, Info, LayoutDashboard, Loader2, Map, Moon, Save, Settings, Sun, Workflow } from '@lucide/svelte';
	import { theme } from '$lib/theme.svelte';

	let { children } = $props();

	const navItems = [
		{ href: '/', label: 'Dashboard', icon: LayoutDashboard },
		{ href: '/devices', label: 'Devices', icon: Cable },
		{ href: '/layout', label: 'Layout', icon: Map },
		{ href: '/editor', label: 'Graph Editor', icon: Workflow },
		{ href: '/settings', label: 'Settings', icon: Settings },
	] as const;

	function isActive(href: string) {
		if (href === '/') return page.url.pathname === '/';
		return page.url.pathname.startsWith(href);
	}
</script>

<svelte:head><link rel="icon" href={favicon} /></svelte:head>

<div class="flex min-h-screen flex-col">
	<header class="sticky top-0 z-50 border-b border-border/60 bg-background/80 backdrop-blur-lg">
		<div class="mx-auto flex h-14 max-w-7xl items-center gap-4 px-4 sm:px-6 lg:px-8">
			<a href="/" class="flex items-center gap-2 text-sm font-semibold tracking-tight">
				<Workflow class="size-5 text-primary" />
				LucaLights
			</a>

			<Separator orientation="vertical" class="!h-6" />

			<nav class="flex items-center gap-1">
				{#each navItems as item}
					<Button
						variant={isActive(item.href) ? 'secondary' : 'ghost'}
						size="sm"
						href={item.href}
					>
						<item.icon class="size-4" />
						{item.label}
					</Button>
				{/each}
			</nav>

			<div class="ml-auto flex items-center gap-2">
				{#if headerActions.help}
					<Button
						variant="ghost"
						size="icon-sm"
						aria-label={headerActions.help.label}
						onclick={headerActions.help.onClick}
						disabled={headerActions.help.disabled}
						title={headerActions.help.title}
					>
						<Info class="size-4" />
					</Button>
				{/if}

				{#if headerActions.primary}
					<Button
						size="sm"
						onclick={headerActions.primary.onClick}
						disabled={headerActions.primary.disabled}
						title={headerActions.primary.title}
					>
						{#if headerActions.primary.busy}
							<Loader2 class="size-4 animate-spin" />
						{:else}
							<Save class="size-4" />
						{/if}
						{headerActions.primary.label}
					</Button>
				{/if}

				<Button variant="ghost" size="sm" onclick={() => theme.toggle()}>
					{#if theme.resolved === 'dark'}
						<Sun class="size-4" />
					{:else}
						<Moon class="size-4" />
					{/if}
				</Button>
			</div>
		</div>
	</header>

	<main class="flex-1">
		{@render children()}
	</main>
</div>
