using System.Text.Json.Serialization;
using LucaLights.Core.Transport;

namespace LucaLights.Core.Models;

public sealed class Device : IDisposable
{
    private Color[] _data = [];
    private DDPSend? _ddpSend;
    private UdpRealtimeSend? _udpRealtimeSend;
    private bool _disposed;

    public Device()
    {
    }

    public Device(string name, string ip, List<Segment> segments, WledProtocol protocol = WledProtocol.DDP)
    {
        Name = name;
        Ip = ip;
        Segments = segments;
        Protocol = protocol;

        Recalculate();
    }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "New Device";

    [JsonPropertyName("ip")]
    public string Ip { get; set; } = "192.168.1.1";

    [JsonPropertyName("protocol")]
    public WledProtocol Protocol { get; set; } = WledProtocol.DDP;

    [JsonPropertyName("segments")]
    public List<Segment> Segments { get; set; } = [];

    [JsonIgnore]
    public int Nsegments { get; private set; }

    [JsonIgnore]
    public int Nleds { get; private set; }

    public void Recalculate()
    {
        ThrowIfDisposed();

        var ledCount = Segments.Sum(segment => segment.Length);
        _data = new Color[ledCount];
        Nleds = ledCount;
        Nsegments = Segments.Count;

        _ddpSend?.Dispose();
        _udpRealtimeSend?.Dispose();
        _ddpSend = null;
        _udpRealtimeSend = null;

        if (ledCount == 0)
        {
            return;
        }

        if (Protocol == WledProtocol.DDP)
        {
            _ddpSend = new DDPSend(Ip, ledCount);
        }
        else
        {
            _udpRealtimeSend = new UdpRealtimeSend(Ip, ledCount);
        }
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

        _ddpSend?.Send(_data);
        _udpRealtimeSend?.Send(_data);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _ddpSend?.Dispose();
        _udpRealtimeSend?.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
