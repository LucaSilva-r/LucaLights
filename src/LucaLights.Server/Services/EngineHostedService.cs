using LucaLights.Core.Configuration;
using LucaLights.Core.Engine;
using LucaLights.Core.GameInput;
using LucaLights.Core.GameInput.Modules;
using LucaLights.Core.Models;

namespace LucaLights.Server.Services;

public sealed class EngineHostedService : IHostedService
{
    private readonly Settings _settings;
    private readonly ConfigManager _configManager;
    private readonly GameInputManager _gameInputManager;
    private readonly LightingManager _lightingManager;
    private readonly ILogger<EngineHostedService> _logger;

    public EngineHostedService(
        Settings settings,
        ConfigManager configManager,
        GameInputManager gameInputManager,
        LightingManager lightingManager,
        ILogger<EngineHostedService> logger)
    {
        _settings = settings;
        _configManager = configManager;
        _gameInputManager = gameInputManager;
        _lightingManager = lightingManager;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting LucaLights runtime.");

        _settings.Normalize();
        _gameInputManager.RegisterModule(ITGManiaInputModule.CreateFromSettings(
            _settings,
            message => _logger.LogInformation("{Message}", message)));
        _gameInputManager.RegisterModule(OsuInputModule.CreateFromSettings(
            _settings,
            message => _logger.LogInformation("{Message}", message)));

        await _gameInputManager.StartAsync(_settings, cancellationToken);
        _lightingManager.Start();

        _logger.LogInformation(
            "LucaLights runtime started with active input module {ModuleId}.",
            _gameInputManager.ActiveModuleId ?? "(none)");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping LucaLights runtime.");

        _lightingManager.Stop();
        await _gameInputManager.StopAsync(CancellationToken.None);
        _configManager.Save(_settings);

        _logger.LogInformation("LucaLights runtime stopped.");
    }
}
