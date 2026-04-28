using System.Text.Json.Serialization;

namespace LucaLights.Core.Models;

public sealed class RoomLayout
{
    [JsonPropertyName("placements")]
    public List<SegmentPlacement> Placements { get; set; } = [];

    public void Normalize(IReadOnlyList<Device> devices)
    {
        Placements ??= [];

        var segmentIds = devices
            .SelectMany(device => device.Segments)
            .Select(segment => segment.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Placements.RemoveAll(placement => string.IsNullOrWhiteSpace(placement.SegmentId)
            || !segmentIds.Contains(placement.SegmentId));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = Placements.Count - 1; i >= 0; i--)
        {
            if (!seen.Add(Placements[i].SegmentId))
            {
                Placements.RemoveAt(i);
            }
        }

        foreach (var placement in Placements)
        {
            placement.Normalize();
        }
    }

    public SegmentPlacement? FindPlacement(string segmentId)
    {
        return Placements.FirstOrDefault(
            placement => string.Equals(placement.SegmentId, segmentId, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class SegmentPlacement
{
    [JsonPropertyName("segmentId")]
    public string SegmentId { get; set; } = string.Empty;

    [JsonPropertyName("x")]
    public float X { get; set; } = 0.5f;

    [JsonPropertyName("y")]
    public float Y { get; set; } = 0.5f;

    [JsonPropertyName("rotation")]
    public float Rotation { get; set; }

    [JsonPropertyName("scaleX")]
    public float ScaleX { get; set; } = 0.25f;

    [JsonPropertyName("scaleY")]
    public float ScaleY { get; set; } = 0.25f;

    public void Normalize()
    {
        SegmentId = SegmentId?.Trim() ?? string.Empty;
        X = NormalizeFinite(X, 0.5f);
        Y = NormalizeFinite(Y, 0.5f);
        Rotation = NormalizeFinite(Rotation, 0f);
        ScaleX = Math.Clamp(Math.Abs(NormalizeFinite(ScaleX, 0.25f)), 0.001f, 10f);
        ScaleY = Math.Clamp(Math.Abs(NormalizeFinite(ScaleY, 0.25f)), 0.001f, 10f);
    }

    private static float NormalizeFinite(float value, float fallback)
    {
        return float.IsFinite(value) ? value : fallback;
    }
}
