namespace LucaLights.Core.GameInput;

public sealed class InputSnapshot
{
    public static InputSnapshot Empty { get; } = new()
    {
        IsConnected = false,
        IsActive = false
    };

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    public long Sequence { get; init; }

    public bool IsConnected { get; init; }

    public bool IsActive { get; init; }

    public IReadOnlyDictionary<string, bool> BoolValues { get; init; } =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, float> FloatValues { get; init; } =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, Color> ColorValues { get; init; } =
        new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public bool GetBool(string key, bool defaultValue = false)
    {
        return BoolValues.TryGetValue(key, out var value) ? value : defaultValue;
    }

    public float GetFloat(string key, float defaultValue = 0f)
    {
        return FloatValues.TryGetValue(key, out var value) ? value : defaultValue;
    }

    public Color GetColor(string key, Color defaultValue)
    {
        return ColorValues.TryGetValue(key, out var value) ? value : defaultValue;
    }

    public string? GetMetadata(string key)
    {
        return Metadata.TryGetValue(key, out var value) ? value : null;
    }
}
