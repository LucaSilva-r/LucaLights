# LucaLights UI

This app is the Phase 3 SvelteKit frontend for LucaLights v2.

It currently provides:

- live runtime status
- input snapshot inspection
- device and effect inventory views
- preview rendering through `/ws/preview`

## Development

The frontend expects the v2 ASP.NET Core server to be available on `http://127.0.0.1:5050` by default.

You can override that with `LUCALIGHTS_BACKEND_URL` in a local `.env`.

Useful commands:

```sh
npm run dev
npm run check
npm run build
```

## VS Code

From the repo root workspace:

- run the `Launch LucaLights v2 Server` launch config to start the backend on `http://127.0.0.1:5050`
- run the `dev v2 ui` task to start the Svelte dev server on `http://127.0.0.1:5173`

The Vite dev server proxies `/api` and `/ws` to the backend, so the browser app can use the same relative URLs that production integration will use.

## Current next step

Add active-effect selection and the first SvelteFlow graph editor shell, then wire the final build output into `LucaLights.Server/wwwroot`.
