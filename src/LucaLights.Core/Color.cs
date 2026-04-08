namespace LucaLights.Core;

public readonly record struct Color(byte R, byte G, byte B)
{
    public static Color Black => new(0, 0, 0);

    public static Color FromRgb(byte r, byte g, byte b) => new(r, g, b);

    public Color Add(Color other)
    {
        return new Color(
            (byte)Math.Clamp(R + other.R, 0, byte.MaxValue),
            (byte)Math.Clamp(G + other.G, 0, byte.MaxValue),
            (byte)Math.Clamp(B + other.B, 0, byte.MaxValue));
    }
}
