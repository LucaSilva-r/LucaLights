using System.Text.Json.Serialization;
using LucaLights.Core.Transport;

namespace LucaLights.Core.Models;

public sealed class Device : IDisposable
{
    private Color[] _data = [];
    private bool _disposed;

    public Device()
    {
    }

    public Device(string name, string ip, List<Segment> segments, DeviceTransportType transportType = DeviceTransportType.DDP)
    {
        Name = name;
        Ip = ip;
        Segments = segments;
        TransportType = transportType;

        Recalculate();
    }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "New Device";

    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("ip")]
    public string Ip { get; set; } = "192.168.1.1";

    [JsonPropertyName("protocol")]
    public DeviceTransportType TransportType { get; set; } = DeviceTransportType.DDP;

    [JsonPropertyName("segments")]
    public List<Segment> Segments { get; set; } = [];

    [JsonIgnore]
    public int Nsegments { get; private set; }

    [JsonIgnore]
    public int Nleds { get; private set; }

    [JsonIgnore]
    public DeviceTransport? Transport { get; private set; }

    public void Recalculate()
    {
        ThrowIfDisposed();

        var ledCount = Segments.Sum(segment => segment.Length);
        _data = new Color[ledCount];
        Nleds = ledCount;
        Nsegments = Segments.Count;

        Transport?.Dispose();
        Transport = null;

        if (ledCount == 0)
        {
            return;
        }

        Transport = DeviceTransport.Create(TransportType, Ip, ledCount);
    }

    public void Send()
    {
        ThrowIfDisposed();

        var offset = 0;
        foreach (var segment in Segments)
        {
            Array.Copy(segment.Leds, 0, _data, offset, segment.Leds.Length);
            offset += segment.Leds.Length;
        }

        Transport?.Send(_data);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Transport?.Dispose();
        Transport = null;
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
