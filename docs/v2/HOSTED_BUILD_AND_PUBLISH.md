# Hosted Build And Publish

This guide describes the verified LucaLights v2 server build and publish flow after the browser UI was integrated into `LucaLights.Server`.

## What The Server Build Does

When you build `src/LucaLights.Server/LucaLights.Server.csproj`:

- the server project runs the Svelte frontend build from `web/lucalights-ui`
- the built frontend files are copied into `src/LucaLights.Server/wwwroot`
- `dotnet run` and local source builds serve those generated files directly

When you publish the same project:

- the publish step copies the frontend build output into `<publish-dir>/wwwroot`
- the published server serves the frontend from that physical publish directory

Generated frontend assets under `src/LucaLights.Server/wwwroot` are build artifacts and should stay untracked in git.

## Prerequisites

- .NET 10 SDK
- Node.js and npm available on `PATH`

The server build now depends on a working Node/npm toolchain because the frontend build is part of the host project.

## Local Source Build

Build the hosted server project:

```bash
dotnet build src/LucaLights.Server/LucaLights.Server.csproj
```

This should:

- restore and build the .NET server
- build the Svelte frontend
- generate hosted frontend assets under `src/LucaLights.Server/wwwroot`

Run the server from source:

```bash
dotnet run --project src/LucaLights.Server
```

Useful smoke-test expectations:

- `GET /` or `HEAD /` returns `200`
- `GET /editor` returns `200`
- `GET /devices` returns `200`
- `GET /api/not-real` returns `404`

## Publish Output

Create a publish directory:

```bash
dotnet publish src/LucaLights.Server/LucaLights.Server.csproj -o /tmp/lucalights-server-publish
```

After publish completes, the output directory should contain:

- the published server binaries
- `wwwroot/index.html`
- `wwwroot/_app/...` frontend assets

Run the published app from the publish directory:

```bash
cd /tmp/lucalights-server-publish
dotnet ./LucaLights.Server.dll
```

Running from the publish directory keeps ASP.NET content-root resolution aligned with the copied `wwwroot` folder. If you launch the published app from somewhere else, set the content root explicitly.

Useful smoke-test expectations:

- `GET /` or `HEAD /` returns `200`
- `GET /editor` returns `200`
- `GET /devices` returns `200`
- `GET /api/not-real` returns `404`

## Routing Notes

- Browser routes such as `/`, `/editor`, and `/devices` fall back to `index.html`
- Unmatched `/api/*` and `/ws/*` routes do not fall back to the SPA and should return `404`
- Non-`GET` and non-`HEAD` requests also do not use the SPA fallback

## Troubleshooting

- If `dotnet build` fails during frontend generation, verify `node` and `npm` are installed and available on `PATH`
- If the server starts but browser routes return a message about missing frontend assets, rebuild `LucaLights.Server` so `wwwroot` is regenerated
- If a published app serves APIs but not the SPA, confirm you are running it from the publish directory and that `<publish-dir>/wwwroot/index.html` exists
