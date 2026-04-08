namespace LucaLights.Core.GameInput;

public sealed class InputChannelDefinition
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public InputValueType ValueType { get; set; }

    public string Category { get; set; } = "General";

    public string Description { get; set; } = string.Empty;

    public float? DefaultFloatValue { get; set; }

    public float? MinFloatValue { get; set; }

    public float? MaxFloatValue { get; set; }
}
