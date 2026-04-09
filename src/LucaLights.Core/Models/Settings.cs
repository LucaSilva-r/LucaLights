using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace LucaLights.Core.Models;

public sealed class Settings
{
    public const string DefaultInputModuleId = "itgmania";

    public Settings()
    {
        EnsureDefaults();
    }

    [JsonPropertyName("devices")]
    public List<Device> Devices { get; set; } = [];

    [JsonPropertyName("effects")]
    public List<Effect> Effects { get; set; } = [];

    [JsonPropertyName("activeEffectId")]
    public string? ActiveEffectId { get; set; }

    [JsonPropertyName("activeInputModuleId")]
    public string ActiveInputModuleId { get; set; } = DefaultInputModuleId;

    [JsonPropertyName("inputModuleSettings")]
    public Dictionary<string, JsonObject> InputModuleSettings { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool Dirty { get; private set; } = true;

    public void Normalize()
    {
        Devices ??= [];
        Effects ??= [];
        ActiveEffectId = string.IsNullOrWhiteSpace(ActiveEffectId) ? null : ActiveEffectId.Trim();
        ActiveInputModuleId = string.IsNullOrWhiteSpace(ActiveInputModuleId)
            ? DefaultInputModuleId
            : ActiveInputModuleId;
        InputModuleSettings = InputModuleSettings is null
            ? new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, JsonObject>(InputModuleSettings, StringComparer.OrdinalIgnoreCase);

        if (Effects.Count == 0)
        {
            ActiveEffectId = null;
        }
        else if (string.IsNullOrWhiteSpace(ActiveEffectId)
            || !Effects.Any(effect => string.Equals(effect.Id, ActiveEffectId, StringComparison.OrdinalIgnoreCase)))
        {
            ActiveEffectId = Effects[0].Id;
        }

        EnsureDefaults();
    }

    public void MarkDirty()
    {
        Dirty = true;
    }

    public void ClearDirty()
    {
        Dirty = false;
    }

    public JsonObject GetOrCreateInputModuleSettings(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        if (!InputModuleSettings.TryGetValue(moduleId, out var moduleSettings) || moduleSettings is null)
        {
            moduleSettings = new JsonObject();
            InputModuleSettings[moduleId] = moduleSettings;
        }

        return moduleSettings;
    }

    private void EnsureDefaults()
    {
        if (!InputModuleSettings.ContainsKey(DefaultInputModuleId))
        {
            InputModuleSettings[DefaultInputModuleId] = new JsonObject
            {
                ["pipeName"] = GetDefaultItgPipeName()
            };
        }
    }

    private static string GetDefaultItgPipeName()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "StepMania-Lights-SextetStream"
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".itgmania/Save/StepMania-Lights-SextetStream.out");
    }
}
