# LucaLights v2 Docs

This folder is the source of truth for the LucaLights v2 rewrite work.

Use these files together:

- [`PLAN.md`](../../PLAN.md): strategic target state and phased architecture plan
- [`EXECUTION_TRACKER.md`](./EXECUTION_TRACKER.md): current implementation status, milestones, risks, and next slices
- [`ARCHITECTURE_GAME_INPUTS.md`](./ARCHITECTURE_GAME_INPUTS.md): canonical design for game-agnostic input modules
- [`ARCHITECTURE_GRAPH_BINDINGS.md`](./ARCHITECTURE_GRAPH_BINDINGS.md): design for reusable graph inputs and binding profiles
- [`ARCHITECTURE_NODE_ENGINE.md`](./ARCHITECTURE_NODE_ENGINE.md): design for node catalog, graph validation, and graph compilation
- [`WORK_LOG.md`](./WORK_LOG.md): append-only session log for progress across multiple chats

## Working Rules

When making v2 progress:

1. Update the execution tracker if a milestone changes state.
2. Append a short entry to the work log at the end of the session.
3. Record architecture changes in the relevant doc instead of leaving them only in chat history.
4. Keep `PLAN.md` focused on direction and phases, and keep day-to-day progress in `docs/v2/`.

## Git Checkpoints

To keep the rewrite reviewable:

1. Make one commit per implementation slice.
2. Include tracker/work-log updates in the same commit as the code they describe.
3. Avoid mixing unrelated extraction, refactor, and feature work in one checkpoint.
4. Prefer commit messages in the form `v2: <slice outcome>`.

## Current Focus

The current recommended starting point is the next Phase 3 slice:

- add custom SvelteFlow node components per node type (color pickers, input selectors, etc.)
- add a node palette / drag-to-add for the graph editor
- integrate the Svelte build into `LucaLights.Server` static assets once the editor shell is stable
