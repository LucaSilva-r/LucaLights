namespace LucaLights.Core.Transport;

public enum DeviceTransportType
{
    DDP,
    UdpRealtime
}

public abstract class DeviceTransport : IDisposable
{
    protected DeviceTransport(string ipAddress, int ledCount)
    {
        IpAddress = ipAddress;
        LedCount = ledCount;
    }

    public string IpAddress { get; }

    public int LedCount { get; }

    public abstract void Send(Color[] leds);

    public abstract void Dispose();

    public static DeviceTransport Create(DeviceTransportType transportType, string ipAddress, int ledCount)
    {
        return transportType switch
        {
            DeviceTransportType.DDP => new DDPSend(ipAddress, ledCount),
            DeviceTransportType.UdpRealtime => new UdpRealtimeSend(ipAddress, ledCount),
            _ => throw new ArgumentOutOfRangeException(nameof(transportType), transportType, "Unsupported device transport")
        };
    }
}
