using LucaLights.Core.Configuration;
using LucaLights.Core.Engine;
using LucaLights.Core.GameInput;
using LucaLights.Core.Models;
using LucaLights.Core.NodeEngine;
using LucaLights.Server.Endpoints;
using LucaLights.Server.Services;
using Microsoft.Extensions.FileProviders;

namespace LucaLights.Server;

public static class LucaLightsServerHost
{
    public static WebApplication BuildApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var resolvedWebRoot = ResolveWebRoot(builder.Environment.ContentRootPath);
        Directory.CreateDirectory(resolvedWebRoot);
        builder.WebHost.UseWebRoot(resolvedWebRoot);

        builder.Services.AddSingleton<ConfigManager>();
        builder.Services.AddSingleton(provider =>
        {
            var settings = provider.GetRequiredService<ConfigManager>().Load();
            settings.Normalize();
            return settings;
        });
        builder.Services.AddSingleton(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<GameInputManager>>();
            return new GameInputManager(message => logger.LogInformation("{Message}", message));
        });
        builder.Services.AddSingleton<LightingManagerOptions>();
        builder.Services.AddSingleton<INodeTypeCatalog, DefaultNodeTypeCatalog>();
        builder.Services.AddSingleton<NodeGraphCompiler>();
        builder.Services.AddSingleton<GraphRuntimeEvaluator>();
        builder.Services.AddSingleton<ILightingRenderer, NodeGraphLightingRenderer>();
        builder.Services.AddSingleton<RuntimeEventBroadcaster>();
        builder.Services.AddSingleton(provider =>
        {
            var settings = provider.GetRequiredService<Settings>();
            var renderer = provider.GetRequiredService<ILightingRenderer>();
            var options = provider.GetRequiredService<LightingManagerOptions>();
            var inputManager = provider.GetRequiredService<GameInputManager>();
            var logger = provider.GetRequiredService<ILogger<LightingManager>>();

            return new LightingManager(
                settings,
                renderer,
                options,
                inputManager,
                shouldSendOutput: () => true,
                log: message => logger.LogInformation("{Message}", message));
        });
        builder.Services.AddHostedService<RuntimeEventHostedService>();
        builder.Services.AddSingleton<PreviewBroadcaster>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<PreviewBroadcaster>());
        builder.Services.AddHostedService<EngineHostedService>();

        var app = builder.Build();
        var webRootFileProvider = new PhysicalFileProvider(app.Environment.WebRootPath);

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseWebSockets();

        app.MapGet(
            "/api",
            (LightingManager lightingManager, GameInputManager inputManager) => Results.Ok(
                new
                {
                    app = "LucaLights.Server",
                    status = "running",
                    lightingRunning = lightingManager.IsRunning,
                    activeInputModule = inputManager.ActiveModuleId
                }));

        app.MapGet(
            "/api/system/status",
            (Settings settings, LightingManager lightingManager, GameInputManager inputManager) =>
            {
                var snapshot = inputManager.LatestSnapshot;

                return Results.Ok(new
                {
                    lighting = new
                    {
                        running = lightingManager.IsRunning
                    },
                    settings = new
                    {
                        devices = settings.Devices.Count,
                        graphNodes = settings.Graph.Nodes.Count,
                        graphConnections = settings.Graph.Connections.Count,
                        activeInputModuleId = settings.ActiveInputModuleId,
                        dirty = settings.Dirty
                    },
                    input = new
                    {
                        activeModuleId = inputManager.ActiveModuleId,
                        connected = snapshot.IsConnected,
                        active = snapshot.IsActive,
                        sequence = snapshot.Sequence,
                        timestampUtc = snapshot.TimestampUtc
                    }
                });
            });

        app.MapGet(
            "/api/input-modules",
            (GameInputManager inputManager) => Results.Ok(inputManager.GetDefinitions()));

        app.MapGet(
            "/api/input-state",
            (GameInputManager inputManager) => Results.Ok(inputManager.LatestSnapshot));

        app.MapSettingsEndpoints();
        app.MapGraphEndpoints();
        app.MapSystemEndpoints();
        app.MapRuntimeWebSockets();
        app.MapFallback(async context =>
        {
            if (context.Request.Path.StartsWithSegments("/api")
                || context.Request.Path.StartsWithSegments("/ws")
                || (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var spaIndexFile = webRootFileProvider.GetFileInfo("index.html");

            if (!spaIndexFile.Exists)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync("Frontend assets are missing. Build LucaLights.Server to generate hosted static assets.");
                return;
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength = spaIndexFile.Length;

            if (HttpMethods.IsHead(context.Request.Method))
            {
                return;
            }

            await using var stream = spaIndexFile.CreateReadStream();
            await stream.CopyToAsync(context.Response.Body, context.RequestAborted);
        });

        return app;
    }

    private static string ResolveWebRoot(string contentRootPath)
    {
        foreach (var candidate in GetWebRootCandidates(contentRootPath))
        {
            if (File.Exists(Path.Combine(candidate, "index.html")))
            {
                return candidate;
            }
        }

        return Path.Combine(contentRootPath, "wwwroot");
    }

    private static IEnumerable<string> GetWebRootCandidates(string contentRootPath)
    {
        var candidates = new[]
        {
            Path.Combine(contentRootPath, "wwwroot"),
            Path.Combine(contentRootPath, "..", "..", "..", "..", "src", "LucaLights.Server", "wwwroot"),
            Path.Combine(contentRootPath, "..", "..", "..", "..", "web", "lucalights-ui", "build"),
            Path.Combine(contentRootPath, "..", "..", "..", "..", "src", "LucaLights.Server", "obj", "BundledServerPublish", "wwwroot")
        };

        foreach (var candidate in candidates)
        {
            yield return Path.GetFullPath(candidate);
        }
    }
}
