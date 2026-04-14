<script lang="ts">
	import './layout.css';
	import favicon from '$lib/assets/favicon.svg';
	import { page } from '$app/state';
	import { Button } from '$lib/components/ui/button';
	import { Separator } from '$lib/components/ui/separator';
	import { Cable, LayoutDashboard, Moon, Settings, Sun, Workflow } from '@lucide/svelte';
	import { theme } from '$lib/theme.svelte';

	let { children } = $props();

	const navItems = [
		{ href: '/', label: 'Dashboard', icon: LayoutDashboard },
		{ href: '/devices', label: 'Devices', icon: Cable },
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

			<div class="ml-auto">
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
