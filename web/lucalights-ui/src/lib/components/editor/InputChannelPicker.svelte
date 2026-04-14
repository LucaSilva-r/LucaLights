<script lang="ts">
	import { Check, ChevronDown, Search, X } from '@lucide/svelte';
	import * as Dialog from '$lib/components/ui/dialog';
	import type { InputChannelDefinition } from '$lib/lucalights';

	type GroupedChannels = {
		key: string;
		label: string;
		channels: InputChannelDefinition[];
	};

	type GroupedModules = {
		key: string;
		label: string;
		categories: GroupedChannels[];
	};

	let {
		value = '',
		channels = [],
		onChange
	}: {
		value?: string;
		channels?: InputChannelDefinition[];
		onChange?: (value: string) => void;
	} = $props();

	let open = $state(false);
	let search = $state('');
	let openModuleKeys = $state<string[]>([]);
	let openCategoryKeys = $state<string[]>([]);

	let normalizedChannels = $derived.by(() =>
		channels.filter(
			(channel) =>
				typeof channel.key === 'string' &&
				channel.key.trim().length > 0 &&
				typeof channel.label === 'string' &&
				channel.label.trim().length > 0
		)
	);

	let channelMap = $derived.by(
		() => new Map(normalizedChannels.map((channel) => [channel.key.toLowerCase(), channel]))
	);

	let selectedKeys = $derived.by(() => parseKeys(value));
	let selectedKeySet = $derived.by(() => new Set(selectedKeys.map((key) => key.toLowerCase())));

	let selectedChannels = $derived.by(() =>
		selectedKeys.map((key) => channelMap.get(key.toLowerCase()) ?? fallbackChannel(key))
	);

	let visibleModules = $derived.by(() => {
		const modules = new Map<
			string,
			{
				key: string;
				label: string;
				categories: Map<string, GroupedChannels>;
			}
		>();
		const query = search.trim().toLowerCase();

		for (const channel of normalizedChannels) {
			const groupLabel = groupLabelFor(channel);
			const moduleKey = moduleKeyFor(channel);
			const moduleLabel = moduleLabelFor(channel);
			const matchesSearch =
				query.length === 0 ||
				channel.label.toLowerCase().includes(query) ||
				channel.key.toLowerCase().includes(query) ||
				groupLabel.toLowerCase().includes(query) ||
				moduleLabel.toLowerCase().includes(query) ||
				channel.description.toLowerCase().includes(query);

			if (!matchesSearch) {
				continue;
			}

			let moduleEntry = modules.get(moduleKey);
			if (!moduleEntry) {
				moduleEntry = {
					key: moduleKey,
					label: moduleLabel,
					categories: new Map<string, GroupedChannels>()
				};
				modules.set(moduleKey, moduleEntry);
			}

			const categoryKey = categoryKeyFor(moduleKey, groupLabel);
			const existingCategory = moduleEntry.categories.get(categoryKey);
			if (existingCategory) {
				existingCategory.channels.push(channel);
			} else {
				moduleEntry.categories.set(categoryKey, {
					key: categoryKey,
					label: groupLabel,
					channels: [channel]
				});
			}
		}

		return Array.from(modules.values()).map(
			(moduleEntry): GroupedModules => ({
				key: moduleEntry.key,
				label: moduleEntry.label,
				categories: Array.from(moduleEntry.categories.values())
			})
		);
	});

	$effect(() => {
		if (!open) {
			search = '';
			return;
		}

		if (openModuleKeys.length === 0 && visibleModules.length > 0) {
			openModuleKeys = defaultOpenModuleKeys();
		}

		if (openCategoryKeys.length === 0 && visibleModules.length > 0) {
			openCategoryKeys = defaultOpenCategoryKeys();
		}
	});

	function parseKeys(input: string) {
		return input
			.split(',')
			.map((part) => part.trim())
			.filter((part, index, values) => part.length > 0 && values.indexOf(part) === index);
	}

	function fallbackChannel(key: string): InputChannelDefinition {
		return {
			key,
			label: key,
			group: 'Selected',
			valueType: 'String',
			category: 'Unknown',
			description: key
		};
	}

	function groupLabelFor(channel: InputChannelDefinition) {
		return channel.group?.trim() || channel.category?.trim() || 'Other';
	}

	function moduleKeyFor(channel: InputChannelDefinition) {
		return channel.moduleId?.trim() || 'active-module';
	}

	function moduleLabelFor(channel: InputChannelDefinition) {
		return channel.moduleDisplayName?.trim() || channel.moduleId?.trim() || 'Active Module';
	}

	function categoryKeyFor(moduleKey: string, categoryLabel: string) {
		return `${moduleKey}::${categoryLabel.toLowerCase()}`;
	}

	function selectionLabel() {
		if (selectedChannels.length === 0) {
			return 'Choose channels';
		}

		if (selectedChannels.length === 1) {
			return selectedChannels[0]?.label ?? '1 channel selected';
		}

		return `${selectedChannels.length} channels selected`;
	}

	function toggleSelection(key: string) {
		const nextKeys = [...selectedKeys];
		const matchIndex = nextKeys.findIndex((candidate) => candidate.toLowerCase() === key.toLowerCase());

		if (matchIndex >= 0) {
			nextKeys.splice(matchIndex, 1);
		} else {
			nextKeys.push(key);
		}

		onChange?.(nextKeys.join(', '));
	}

	function clearSelection() {
		onChange?.('');
	}

	function selectionTooltip() {
		if (selectedChannels.length === 0) {
			return 'No channels selected';
		}

		return selectedChannels.map((channel) => `${channel.label} · ${channel.key}`).join('\n');
	}

	function defaultOpenModuleKeys() {
		const selectedModuleKeys = Array.from(
			new Set(selectedChannels.map((channel) => moduleKeyFor(channel)))
		);

		if (selectedModuleKeys.length > 0) {
			return selectedModuleKeys;
		}

		return visibleModules.length > 0 ? [visibleModules[0].key] : [];
	}

	function defaultOpenCategoryKeys() {
		const selectedCategoryKeys = Array.from(
			new Set(
				selectedChannels.map((channel) =>
					categoryKeyFor(moduleKeyFor(channel), groupLabelFor(channel))
				)
			)
		);

		if (selectedCategoryKeys.length > 0) {
			return selectedCategoryKeys;
		}

		const firstModule = visibleModules[0];
		const firstCategory = firstModule?.categories[0];
		return firstModule && firstCategory ? [firstCategory.key] : [];
	}

	function moduleContainsSelection(moduleKey: string) {
		return selectedChannels.some((channel) => moduleKeyFor(channel) === moduleKey);
	}

	function categoryContainsSelection(categoryKey: string) {
		return selectedChannels.some(
			(channel) => categoryKeyFor(moduleKeyFor(channel), groupLabelFor(channel)) === categoryKey
		);
	}

	function isModuleOpen(moduleKey: string) {
		return search.trim().length > 0 || openModuleKeys.includes(moduleKey);
	}

	function isCategoryOpen(categoryKey: string) {
		return search.trim().length > 0 || openCategoryKeys.includes(categoryKey);
	}

	function toggleModule(moduleKey: string) {
		if (search.trim().length > 0) {
			return;
		}

		openModuleKeys = openModuleKeys.includes(moduleKey) ? [] : [moduleKey];
	}

	function toggleCategory(categoryKey: string) {
		if (search.trim().length > 0) {
			return;
		}

		openCategoryKeys = openCategoryKeys.includes(categoryKey)
			? openCategoryKeys.filter((value) => value !== categoryKey)
			: [...openCategoryKeys, categoryKey];
	}
</script>

<Dialog.Root bind:open>
	<div class="w-full">
		<button
			type="button"
			class="nodrag nopan flex h-7 w-full items-center gap-2 rounded-md border border-border/70 bg-background/90 px-2 text-left text-[11px] shadow-sm outline-none transition hover:border-primary/40 focus:border-ring focus:ring-4 focus:ring-ring/20"
			title={selectionTooltip()}
			onclick={() => {
				open = true;
			}}
		>
			<span class="min-w-0 flex-1 truncate">{selectionLabel()}</span>
			{#if selectedChannels.length > 0}
				<span class="rounded-full border border-border/70 bg-secondary px-1.5 py-0.5 text-[10px] text-secondary-foreground">
					{selectedChannels.length}
				</span>
			{/if}
			<ChevronDown class="size-3.5 text-muted-foreground" />
		</button>
	</div>

		<Dialog.Content class="max-h-[85vh] w-[min(56rem,calc(100vw-1rem))] overflow-hidden border-surface-card-border bg-background p-0 sm:max-w-[56rem]">
		<div class="flex max-h-[85vh] flex-col overflow-hidden">
			<div class="border-b border-border/60 px-5 py-5 pr-12">
				<Dialog.Header class="gap-2">
					<Dialog.Title class="text-base">Input Channels</Dialog.Title>
					<Dialog.Description>
						Grouped by input module so large game integrations stay manageable.
					</Dialog.Description>
				</Dialog.Header>

				<div class="mt-4 flex items-center gap-3">
					<label class="relative min-w-0 flex-1">
						<Search class="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
						<input
							class="nodrag nopan h-10 w-full rounded-xl border border-border/70 bg-background/80 pl-10 pr-3 text-sm shadow-sm outline-none transition focus:border-ring focus:ring-4 focus:ring-ring/20"
							type="text"
							placeholder="Search channels or groups"
							bind:value={search}
						/>
					</label>

					{#if selectedChannels.length > 0}
						<button
							type="button"
							class="rounded-lg border border-border/70 px-3 py-2 text-xs text-muted-foreground transition hover:border-primary/30 hover:text-foreground"
							onclick={clearSelection}
						>
							Clear
						</button>
					{/if}
				</div>
			</div>

			{#if selectedChannels.length > 0}
				<div class="border-b border-border/60 px-5 py-4">
					<div class="flex items-center justify-between gap-3">
						<p class="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
							Selected
						</p>
						<p class="text-[11px] text-muted-foreground">{selectedChannels.length} chosen</p>
					</div>

					<div class="mt-3 flex flex-wrap gap-2">
						{#each selectedChannels as channel}
							<button
								type="button"
								class="flex max-w-full items-center gap-1 rounded-full border border-primary/20 bg-primary/10 px-3 py-1.5 text-xs text-primary transition hover:border-primary/40 hover:bg-primary/15"
								title={`${channel.label} · ${channel.key}`}
								onclick={() => toggleSelection(channel.key)}
							>
								<span class="truncate">{channel.label}</span>
								<X class="size-3 shrink-0" />
							</button>
						{/each}
					</div>
				</div>
			{/if}

				<div class="min-h-0 flex-1 overflow-y-auto overflow-x-hidden px-5 py-5">
					{#if visibleModules.length === 0}
						<p class="rounded-xl border border-dashed border-border/70 px-4 py-10 text-center text-sm text-muted-foreground">
							No channels match the current search.
						</p>
					{:else}
						<div class="space-y-6">
							{#each visibleModules as module}
								<section class="rounded-2xl border border-border/60 bg-background">
									<button
										type="button"
										class="flex w-full items-center justify-between gap-3 px-4 py-3 text-left transition hover:bg-background/40"
										onclick={() => toggleModule(module.key)}
									>
										<div class="min-w-0">
											<p class="text-sm font-semibold text-foreground">{module.label}</p>
											<p class="text-[11px] text-muted-foreground">
												{module.categories.length} categories
											</p>
										</div>
										<div class="flex items-center gap-2">
											{#if moduleContainsSelection(module.key)}
												<span class="rounded-full border border-primary/20 bg-primary/10 px-1.5 py-0.5 text-[10px] text-primary">
													selected
												</span>
											{/if}
											<ChevronDown
												class={`size-4 text-muted-foreground transition ${
													isModuleOpen(module.key) ? 'rotate-180' : ''
												}`}
											/>
										</div>
									</button>

									{#if isModuleOpen(module.key)}
										<div class="space-y-3 border-t border-border/60 px-4 py-4">
											{#each module.categories as category}
												<section class="rounded-xl border border-border/60 bg-card">
													<button
														type="button"
														class="flex w-full items-center justify-between gap-3 px-3 py-2.5 text-left transition hover:bg-background/45"
														onclick={() => toggleCategory(category.key)}
													>
														<div class="min-w-0">
															<p class="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
																{category.label}
															</p>
														</div>
														<div class="flex items-center gap-2">
															{#if categoryContainsSelection(category.key)}
																<span class="rounded-full border border-primary/20 bg-primary/10 px-1.5 py-0.5 text-[10px] text-primary">
																	selected
																</span>
															{/if}
															<span class="rounded-full border border-border/70 bg-background/80 px-1.5 py-0.5 text-[10px] text-muted-foreground">
																{category.channels.length}
															</span>
															<ChevronDown
																class={`size-4 text-muted-foreground transition ${
																	isCategoryOpen(category.key) ? 'rotate-180' : ''
																}`}
															/>
														</div>
													</button>

													{#if isCategoryOpen(category.key)}
														<div class="grid gap-2 border-t border-border/60 px-3 py-3 sm:grid-cols-2">
															{#each category.channels as channel}
																<button
																	type="button"
																	class={`flex min-w-0 items-center justify-between gap-3 rounded-xl border px-3 py-3 text-left transition ${
																		selectedKeySet.has(channel.key.toLowerCase())
																			? 'border-primary/40 bg-primary/10'
																			: 'border-border/70 bg-card hover:border-primary/20 hover:bg-muted'
																	}`}
																	title={`${channel.label} · ${channel.key}`}
																	onclick={() => toggleSelection(channel.key)}
																>
																	<p class="min-w-0 flex-1 truncate text-sm font-medium text-foreground">
																		{channel.label}
																	</p>
																	<div class="flex size-5 shrink-0 items-center justify-center rounded-full border border-border/70 bg-background/80">
																		{#if selectedKeySet.has(channel.key.toLowerCase())}
																			<Check class="size-3 text-primary" />
																		{/if}
																	</div>
																</button>
															{/each}
														</div>
													{/if}
												</section>
											{/each}
										</div>
									{/if}
								</section>
							{/each}
						</div>
					{/if}
			</div>
		</div>
	</Dialog.Content>
</Dialog.Root>
