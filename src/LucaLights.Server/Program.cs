using LucaLights.Core.Configuration;
using LucaLights.Core.Engine;
using LucaLights.Core.GameInput;
using LucaLights.Core.Models;
using LucaLights.Core.NodeEngine;
using LucaLights.Server.Endpoints;
using LucaLights.Server.Services;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSingleton<ILightingRenderer, NoOpLightingRenderer>();
builder.Services.AddSingleton<LightingManagerOptions>();
builder.Services.AddSingleton<INodeTypeCatalog, DefaultNodeTypeCatalog>();
builder.Services.AddSingleton<NodeGraphCompiler>();
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
                effects = settings.Effects.Count,
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

app.Run();
