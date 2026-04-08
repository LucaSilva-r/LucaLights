using System.Net;
using System.Net.Sockets;

namespace LucaLights.Core.Transport;

internal sealed class UdpRealtimeSend : DeviceTransport
{
    private const int UdpRealtimePort = 21324;
    private const int HeaderLength = 2;
    private const int MaxLedCount = 490;
    private const byte DrgbProtocol = 2;

    private readonly UdpClient _client;
    private readonly IPEndPoint _endPoint;
    private readonly byte[] _data;
    private readonly byte _timeout;

    public UdpRealtimeSend(string ipAddress, int ledCount, byte timeout = 2)
        : base(ipAddress, ledCount)
    {
        if (ledCount > MaxLedCount)
        {
            throw new ArgumentException(
                $"UDP Realtime DRGB protocol supports a maximum of {MaxLedCount} LEDs. Requested: {ledCount}",
                nameof(ledCount));
        }

        _timeout = timeout;
        _endPoint = new IPEndPoint(IPAddress.Parse(ipAddress), UdpRealtimePort);
        _client = new UdpClient();
        _data = new byte[HeaderLength + ledCount * 3];
        _data[0] = DrgbProtocol;
        _data[1] = _timeout;
    }

    public override void Send(Color[] leds)
    {
        _data[1] = _timeout;

        for (var index = 0; index < leds.Length; index++)
        {
            var offset = HeaderLength + index * 3;
            _data[offset] = leds[index].R;
            _data[offset + 1] = leds[index].G;
            _data[offset + 2] = leds[index].B;
        }

        _ = _client.SendAsync(_data, _data.Length, _endPoint);
    }

    public override void Dispose()
    {
        _client.Dispose();
    }
}
