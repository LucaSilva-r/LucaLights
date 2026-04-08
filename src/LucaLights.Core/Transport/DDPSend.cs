using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace LucaLights.Core.Transport;

internal sealed class DDPSend : IDisposable
{
    private const int DdpPort = 4048;
    private const byte DdpHeaderLength = 10;
    private const short DdpFlagsVersion1Push = 0x41;
    private const byte DdpTypeDisplay = 1;
    private const byte DdpDisplayId = 1;

    private readonly UdpClient _client;
    private readonly IPEndPoint _endPoint;
    private readonly byte[] _data;
    private byte _sequence;

    public DDPSend(string ipAddress, int ledCount)
    {
        _endPoint = new IPEndPoint(IPAddress.Parse(ipAddress), DdpPort);
        _client = new UdpClient();
        _data = new byte[DdpHeaderLength + ledCount * 3];

        BinaryPrimitives.WriteInt16LittleEndian(_data.AsSpan(0, 2), DdpFlagsVersion1Push);
        _data[2] = DdpTypeDisplay;
        _data[3] = DdpDisplayId;
        BinaryPrimitives.WriteInt32LittleEndian(_data.AsSpan(4, 4), 0);
        BinaryPrimitives.WriteInt16BigEndian(_data.AsSpan(8, 2), (short)(ledCount * 3));
    }

    public void Send(Color[] leds)
    {
        _data[1] = (byte)((_sequence % 15) + 1);
        _sequence++;

        for (var index = 0; index < leds.Length; index++)
        {
            var offset = DdpHeaderLength + index * 3;
            _data[offset] = leds[index].R;
            _data[offset + 1] = leds[index].G;
            _data[offset + 2] = leds[index].B;
        }

        _ = _client.SendAsync(_data, _data.Length, _endPoint);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
