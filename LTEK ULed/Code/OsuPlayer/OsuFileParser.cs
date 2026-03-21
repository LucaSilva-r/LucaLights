using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace LTEK_ULed.Code.OsuPlayer;

public static class OsuFileParser
{
    public static List<ManiaHitObject> Parse(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var hitObjects = new List<ManiaHitObject>();

        int columnCount = 4; // default
        string? currentSection = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Section headers
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                currentSection = trimmed[1..^1];
                continue;
            }

            switch (currentSection)
            {
                case "Difficulty":
                    if (trimmed.StartsWith("CircleSize:"))
                    {
                        var value = trimmed["CircleSize:".Length..].Trim();
                        columnCount = (int)float.Parse(value, CultureInfo.InvariantCulture);
                    }
                    break;

                case "HitObjects":
                    var obj = ParseHitObject(trimmed, columnCount);
                    if (obj != null)
                        hitObjects.Add(obj);
                    break;
            }
        }

        return hitObjects.OrderBy(h => h.StartTimeMs).ThenBy(h => h.Column).ToList();
    }

    private static ManiaHitObject? ParseHitObject(string line, int columnCount)
    {
        var parts = line.Split(',');
        if (parts.Length < 4) return null;

        if (!int.TryParse(parts[0], out var x)) return null;
        if (!int.TryParse(parts[2], out var time)) return null;
        if (!int.TryParse(parts[3], out var type)) return null;

        var column = (int)Math.Floor((double)x * columnCount / 512.0);
        column = Math.Clamp(column, 0, columnCount - 1);

        bool isHold = (type & 128) != 0;
        int endTime = time;

        if (isHold && parts.Length >= 6)
        {
            // Hold note format: x,y,time,type,hitSound,endTime:p1:p2:p3:p4:
            var endParts = parts[5].Split(':');
            if (int.TryParse(endParts[0], out var parsedEnd))
                endTime = parsedEnd;
        }

        // Tap notes need a minimum duration so the 60fps lighting loop can catch them
        const int MinTapDurationMs = 80;
        if (!isHold && endTime == time)
            endTime = time + MinTapDurationMs;

        return new ManiaHitObject
        {
            Column = column,
            StartTimeMs = time,
            EndTimeMs = endTime,
            IsHold = isHold
        };
    }
}
