namespace LucaLights.Core.GameInput;

public sealed class InputDefinition
{
    public string ModuleId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public List<InputChannelDefinition> Channels { get; set; } = [];
}
