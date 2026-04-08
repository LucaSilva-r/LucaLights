namespace LucaLights.Core.Transport;

public enum DeviceProtocolType
{
    DDP,
    UdpRealtime
}

public abstract class WledProtocol : IDisposable
{
    protected WledProtocol(string ipAddress, int ledCount)
    {
        IpAddress = ipAddress;
        LedCount = ledCount;
    }

    public string IpAddress { get; }

    public int LedCount { get; }

    public abstract void Send(Color[] leds);

    public abstract void Dispose();

    public static WledProtocol Create(DeviceProtocolType protocolType, string ipAddress, int ledCount)
    {
        return protocolType switch
        {
            DeviceProtocolType.DDP => new DDPSend(ipAddress, ledCount),
            DeviceProtocolType.UdpRealtime => new UdpRealtimeSend(ipAddress, ledCount),
            _ => throw new ArgumentOutOfRangeException(nameof(protocolType), protocolType, "Unsupported device protocol")
        };
    }
}
