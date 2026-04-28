using System.Text.Json.Serialization;

namespace LucaLights.Core.Models;

public sealed class Segment
{
    private int _length = 1;

    public Segment()
    {
        ResizeLedBuffer();
    }

    public Segment(string name, int length, List<int>? groupIds = null)
    {
        Name = name;
        GroupIds = groupIds ?? [];
        Length = length;
    }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "New Segment";

    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("groupIds")]
    public List<int> GroupIds { get; set; } = [];

    [JsonPropertyName("length")]
    public int Length
    {
        get => _length;
        set
        {
            _length = Math.Max(0, value);
            ResizeLedBuffer();
            NormalizeLayout();
        }
    }

    [JsonPropertyName("layout")]
    public List<LedLayoutPoint> Layout { get; set; } = [];

    [JsonIgnore]
    public Color[] Leds { get; private set; } = [];

    public void ResizeLedBuffer()
    {
        Leds = new Color[_length];
    }

    public void NormalizeLayout()
    {
        Layout ??= [];

        if (Layout.Count > _length)
        {
            Layout.RemoveRange(_length, Layout.Count - _length);
        }

        for (var i = 0; i < Layout.Count; i++)
        {
            Layout[i] = Layout[i].Clamp();
        }

        while (Layout.Count < _length)
        {
            Layout.Add(CreateLinearFallbackPoint(Layout.Count, _length));
        }
    }

    public static LedLayoutPoint CreateLinearFallbackPoint(int index, int length)
    {
        return new LedLayoutPoint(
            length > 1 ? (float)index / (length - 1) : 0f,
            0f);
    }
}

public readonly record struct LedLayoutPoint(
    [property: JsonPropertyName("x")] float X,
    [property: JsonPropertyName("y")] float Y)
{
    public LedLayoutPoint Clamp()
    {
        return new LedLayoutPoint(
            float.IsFinite(X) ? Math.Clamp(X, 0f, 1f) : 0f,
            float.IsFinite(Y) ? Math.Clamp(Y, 0f, 1f) : 0f);
    }
}
