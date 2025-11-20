using System;
using System.Net.Sockets;
using System.Net;
using Avalonia.Media;

namespace LTEK_ULed.Code.Utils
{

    internal class UdpRealtimeSend : IDisposable
    {

        private const int UDP_REALTIME_PORT = 21324;
        private const int UDP_REALTIME_HEADER_LEN = 2;
        private const int UDP_REALTIME_MAX_LEDS = 490;   // Maximum LEDs for DRGB protocol

        private const byte PROTOCOL_DRGB = 2;  // DRGB protocol type

        UdpClient client;
        IPEndPoint endPoint;
        byte[] data;
        byte timeout;

        public UdpRealtimeSend(string ip, int nLeds, byte timeout = 2)
        {
            if (nLeds > UDP_REALTIME_MAX_LEDS)
            {
                throw new ArgumentException($"UDP Realtime DRGB protocol supports a maximum of {UDP_REALTIME_MAX_LEDS} LEDs. Requested: {nLeds}");
            }

            this.timeout = timeout;
            endPoint = new IPEndPoint(IPAddress.Parse(ip), UDP_REALTIME_PORT);

            client = new UdpClient();
            data = new byte[UDP_REALTIME_HEADER_LEN + nLeds * 3];

            // Initialize header
            data[0] = PROTOCOL_DRGB;
            data[1] = timeout;
        }

        public void send(Color[] leds)
        {
            // Update timeout in case it changed (always byte 1)
            data[1] = timeout;

            // Pack RGB data sequentially
            for (int i = 0; i < leds.Length; i++)
            {
                data[UDP_REALTIME_HEADER_LEN + i * 3] = leds[i].R;
                data[UDP_REALTIME_HEADER_LEN + i * 3 + 1] = leds[i].G;
                data[UDP_REALTIME_HEADER_LEN + i * 3 + 2] = leds[i].B;
            }

            client.SendAsync(data, data.Length, endPoint);
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }

}
