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
        }
    }

    [JsonIgnore]
    public Color[] Leds { get; private set; } = [];

    public void ResizeLedBuffer()
    {
        Leds = new Color[_length];
    }
}
