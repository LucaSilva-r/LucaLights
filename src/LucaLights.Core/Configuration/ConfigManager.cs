using System.Text.Json;
using LucaLights.Core.Models;

namespace LucaLights.Core.Configuration;

public sealed class ConfigManager
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true
    };

    public ConfigManager(string? settingsPath = null)
    {
        SettingsPath = settingsPath ?? GetDefaultSettingsPath();
    }

    public string SettingsPath { get; }

    public Settings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new Settings();
        }

        var json = File.ReadAllText(SettingsPath);
        var settings = JsonSerializer.Deserialize<Settings>(json, _serializerOptions) ?? new Settings();
        settings.Normalize();
        return settings;
    }

    public void Save(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.Normalize();

        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, _serializerOptions);
        File.WriteAllText(SettingsPath, json);
    }

    public static string GetDefaultSettingsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LucaLights",
            "settings.json");
    }
}
