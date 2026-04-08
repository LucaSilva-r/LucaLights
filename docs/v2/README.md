# LucaLights v2 Docs

This folder is the source of truth for the LucaLights v2 rewrite work.

Use these files together:

- [`PLAN.md`](../../PLAN.md): strategic target state and phased architecture plan
- [`EXECUTION_TRACKER.md`](./EXECUTION_TRACKER.md): current implementation status, milestones, risks, and next slices
- [`ARCHITECTURE_GAME_INPUTS.md`](./ARCHITECTURE_GAME_INPUTS.md): canonical design for game-agnostic input modules
- [`ARCHITECTURE_GRAPH_BINDINGS.md`](./ARCHITECTURE_GRAPH_BINDINGS.md): design for reusable graph inputs and binding profiles
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

The current recommended starting point is Phase 1:

- add the ASP.NET Core host bootstrap
- start the engine and active input module from config
- expose a thin diagnostics surface for early integration
- keep the browser UI deferred until the backend lifecycle is stable
