using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Color = LucaLights.Core.Color;

namespace LucaLights.Core.GameInput;

// Palette of N colors extracted from an image via median-cut clustering.
// Colors are sorted by a "LED visibility" score (saturation × mid-lightness) so index 0 is
// always the most visible candidate. Slots beyond the cluster count repeat the last color
// rather than going black, so consumers can always pick color_0 and get something lit.
public sealed class CoverArtPalette
{
    public const int Size = 5;

    public static CoverArtPalette Empty { get; } = new(new[] { Color.Black });

    private readonly Color[] _colors;

    public CoverArtPalette(IReadOnlyList<Color> colors)
    {
        _colors = colors.Count > 0 ? colors.ToArray() : new[] { Color.Black };
    }

    // Index beyond the cluster count clamps to the last color (most-visible fallback).
    public Color this[int index] => _colors[Math.Min(index, _colors.Length - 1)];
}

public static class CoverArtPaletteExtractor
{
    private const int ResizeMax = 100;

    // Reject near-transparent and extreme-lightness pixels before clustering so the
    // clusters reflect image content rather than letterboxing or alpha edges.
    private const double MinLightness = 0.04;
    private const double MaxLightness = 0.96;

    // Over-segmentation target. Median-cut alone clusters by pixel volume, so a dominant
    // background hue would consume all N buckets on monochromatic covers. We split much
    // finer than the output size and then farthest-point-sample 5 maximally-different
    // clusters — this captures minority hues (even a handful of pixels) that the user
    // actually wants on lights.
    private const int OverSegmentBuckets = 24;

    public static CoverArtPalette FromFile(string imagePath)
    {
        using var image = Image.Load<Rgba32>(imagePath);
        return FromImage(image);
    }

    public static CoverArtPalette FromImage(Image<Rgba32> image)
    {
        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(ResizeMax, ResizeMax),
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Box
        }));

        var pixels = CollectPixels(image);
        if (pixels.Count == 0) return CoverArtPalette.Empty;

        var buckets = MedianCut(pixels, OverSegmentBuckets)
            .Where(b => b.Count > 0)
            .Select(b => new Cluster(AverageColor(b), b.Count))
            .ToList();

        if (buckets.Count == 0) return CoverArtPalette.Empty;

        var picked = FarthestPointSample(buckets, CoverArtPalette.Size);
        picked.Sort((a, b) => VisibilityScore(b.Color).CompareTo(VisibilityScore(a.Color)));
        return new CoverArtPalette(picked.Select(c => c.Color).ToList());
    }

    private readonly record struct Cluster(Color Color, int Population);

    // Farthest-point sampling: start with the most-populated cluster as anchor, then
    // greedily add clusters whose minimum distance to the already-picked set is largest.
    // Distance is weighted HSL — hue counts more than lightness so diversity is driven by
    // actual color variety, not just brightness steps within a single hue.
    private static List<Cluster> FarthestPointSample(List<Cluster> clusters, int count)
    {
        if (clusters.Count <= count) return new List<Cluster>(clusters);

        var picked = new List<Cluster>(count);
        var anchor = clusters.OrderByDescending(c => c.Population).First();
        picked.Add(anchor);

        while (picked.Count < count)
        {
            double bestMinDist = -1;
            Cluster best = default;
            foreach (var c in clusters)
            {
                if (picked.Contains(c)) continue;
                var minDist = double.MaxValue;
                foreach (var p in picked)
                {
                    var d = ColorDistance(c.Color, p.Color);
                    if (d < minDist) minDist = d;
                }
                if (minDist > bestMinDist)
                {
                    bestMinDist = minDist;
                    best = c;
                }
            }
            if (bestMinDist < 0) break;
            picked.Add(best);
        }
        return picked;
    }

    private static double ColorDistance(Color a, Color b)
    {
        var (ha, sa, la) = RgbToHsl(a.R, a.G, a.B);
        var (hb, sb, lb) = RgbToHsl(b.R, b.G, b.B);
        var dh = Math.Abs(ha - hb);
        if (dh > 0.5) dh = 1.0 - dh; // hue is cyclic
        // Hue weighted 2× — diversity in this extractor means different colors, not
        // different brightnesses of the same color.
        var ds = sa - sb;
        var dl = la - lb;
        return (dh * 2) * (dh * 2) + ds * ds + dl * dl;
    }

    private readonly record struct Rgb(byte R, byte G, byte B);

    private static List<Rgb> CollectPixels(Image<Rgba32> image)
    {
        var pixels = new List<Rgb>(image.Width * image.Height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var px = row[x];
                    if (px.A < 128) continue;
                    var (_, _, l) = RgbToHsl(px.R, px.G, px.B);
                    if (l < MinLightness || l > MaxLightness) continue;
                    pixels.Add(new Rgb(px.R, px.G, px.B));
                }
            }
        });
        return pixels;
    }

    // Median-cut partitions the pixel set into up to targetColors boxes by repeatedly
    // splitting the box with the widest channel range along that channel's median.
    // Each resulting box's centroid becomes a palette color.
    private static List<List<Rgb>> MedianCut(List<Rgb> pixels, int targetColors)
    {
        var buckets = new List<List<Rgb>> { pixels };
        while (buckets.Count < targetColors)
        {
            var splitIdx = -1;
            var widestRange = -1;
            for (var i = 0; i < buckets.Count; i++)
            {
                if (buckets[i].Count < 2) continue;
                var range = WidestChannelRange(buckets[i]);
                if (range > widestRange)
                {
                    widestRange = range;
                    splitIdx = i;
                }
            }
            if (splitIdx < 0) break;

            var bucket = buckets[splitIdx];
            var axis = LongestAxis(bucket);
            bucket.Sort((a, b) => axis switch
            {
                0 => a.R.CompareTo(b.R),
                1 => a.G.CompareTo(b.G),
                _ => a.B.CompareTo(b.B),
            });

            var mid = bucket.Count / 2;
            var left  = bucket.GetRange(0, mid);
            var right = bucket.GetRange(mid, bucket.Count - mid);
            buckets[splitIdx] = left;
            buckets.Add(right);
        }
        return buckets;
    }

    private static int WidestChannelRange(List<Rgb> bucket)
    {
        byte rMin = 255, rMax = 0, gMin = 255, gMax = 0, bMin = 255, bMax = 0;
        for (var i = 0; i < bucket.Count; i++)
        {
            var p = bucket[i];
            if (p.R < rMin) rMin = p.R; if (p.R > rMax) rMax = p.R;
            if (p.G < gMin) gMin = p.G; if (p.G > gMax) gMax = p.G;
            if (p.B < bMin) bMin = p.B; if (p.B > bMax) bMax = p.B;
        }
        return Math.Max(rMax - rMin, Math.Max(gMax - gMin, bMax - bMin));
    }

    private static int LongestAxis(List<Rgb> bucket)
    {
        byte rMin = 255, rMax = 0, gMin = 255, gMax = 0, bMin = 255, bMax = 0;
        for (var i = 0; i < bucket.Count; i++)
        {
            var p = bucket[i];
            if (p.R < rMin) rMin = p.R; if (p.R > rMax) rMax = p.R;
            if (p.G < gMin) gMin = p.G; if (p.G > gMax) gMax = p.G;
            if (p.B < bMin) bMin = p.B; if (p.B > bMax) bMax = p.B;
        }
        var dR = rMax - rMin;
        var dG = gMax - gMin;
        var dB = bMax - bMin;
        if (dR >= dG && dR >= dB) return 0;
        if (dG >= dB)              return 1;
        return 2;
    }

    private static Color AverageColor(List<Rgb> bucket)
    {
        long r = 0, g = 0, b = 0;
        for (var i = 0; i < bucket.Count; i++)
        {
            r += bucket[i].R;
            g += bucket[i].G;
            b += bucket[i].B;
        }
        return Color.FromRgb(
            (byte)(r / bucket.Count),
            (byte)(g / bucket.Count),
            (byte)(b / bucket.Count));
    }

    // LED visibility: peaks at saturated, mid-lightness. Near-gray and near-black/white
    // colors score low so they sort to the back — ensuring color_0 is always the lit pick.
    private static double VisibilityScore(Color c)
    {
        var (_, s, l) = RgbToHsl(c.R, c.G, c.B);
        var lightFactor = 1.0 - Math.Abs(l - 0.5) * 2.0; // 1 at l=0.5, 0 at 0 or 1
        return s * lightFactor;
    }

    private static (double H, double S, double L) RgbToHsl(byte r, byte g, byte b)
    {
        var rf = r / 255.0;
        var gf = g / 255.0;
        var bf = b / 255.0;
        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var l = (max + min) * 0.5;

        double h, s;
        if (Math.Abs(max - min) < 1e-6)
        {
            h = 0;
            s = 0;
        }
        else
        {
            var d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
            if      (max == rf) h = ((gf - bf) / d + (gf < bf ? 6 : 0)) / 6.0;
            else if (max == gf) h = ((bf - rf) / d + 2) / 6.0;
            else                h = ((rf - gf) / d + 4) / 6.0;
        }
        return (h, s, l);
    }
}
