using System.Text.Json.Nodes;
using LucaLights.Core.Engine;
using LucaLights.Core.GameInput;
using LucaLights.Core.Models;
using LucaLights.Core.NodeEngine;

namespace LucaLights.Core.Tests;

public sealed class GraphRuntimeEvaluatorTests
{
    [Fact]
    public void ConstantColorWritesAllSegments()
    {
        var settings = CreateSettings(3, 2);
        settings.Graph = new NodeGraph
        {
            Nodes =
            [
                Node("color", "constant.color", new JsonObject
                {
                    ["r"] = 12,
                    ["g"] = 34,
                    ["b"] = 56
                }),
                Node("out", "output.segment-color")
            ],
            Connections =
            [
                Link("color", "color", "out", "color")
            ]
        };

        Render(settings, TimeSpan.Zero, InputSnapshot.Empty);

        AssertLeds(settings.Devices[0].Segments[0], 12, 34, 56, 12, 34, 56, 12, 34, 56);
        AssertLeds(settings.Devices[0].Segments[1], 12, 34, 56, 12, 34, 56);
    }

    [Fact]
    public void OscillatorCanDriveBrightness()
    {
        var settings = CreateSettings(2);
        settings.Graph = new NodeGraph
        {
            Nodes =
            [
                Node("osc", "time.oscillator", new JsonObject
                {
                    ["speed"] = 1,
                    ["waveform"] = "triangle"
                }),
                Node("brightness", "color.brightness", new JsonObject
                {
                    ["color"] = new JsonObject
                    {
                        ["r"] = 100,
                        ["g"] = 80,
                        ["b"] = 40
                    }
                }),
                Node("out", "output.segment-color")
            ],
            Connections =
            [
                Link("osc", "value", "brightness", "factor"),
                Link("brightness", "color", "out", "color")
            ]
        };

        Render(settings, TimeSpan.FromSeconds(0.25), InputSnapshot.Empty);

        AssertLeds(settings.Devices[0].Segments[0], 50, 40, 20, 50, 40, 20);
    }

    [Fact]
    public void PixelInfoCanSampleGradientPerPixel()
    {
        var settings = CreateSettings(3, 2);
        settings.Graph = new NodeGraph
        {
            Nodes =
            [
                Node("pixel", "pixel.info"),
                Node("gradient", "color.gradient", new JsonObject
                {
                    ["stops"] = "[{\"p\":0,\"r\":0,\"g\":0,\"b\":0},{\"p\":1,\"r\":255,\"g\":0,\"b\":0}]"
                }),
                Node("out", "output.segment-color")
            ],
            Connections =
            [
                Link("pixel", "normalized", "gradient", "factor"),
                Link("gradient", "color", "out", "color")
            ]
        };

        Render(settings, TimeSpan.Zero, InputSnapshot.Empty);

        AssertLeds(settings.Devices[0].Segments[0], 0, 0, 0, 128, 0, 0, 255, 0, 0);
        AssertLeds(settings.Devices[0].Segments[1], 0, 0, 0, 255, 0, 0);
    }

    [Fact]
    public void SegmentOutputsApplyInPriorityOrderWithBlendMode()
    {
        var settings = CreateSettings(2);
        settings.Graph = new NodeGraph
        {
            Nodes =
            [
                Node("red", "constant.color", new JsonObject
                {
                    ["r"] = 255,
                    ["g"] = 0,
                    ["b"] = 0
                }),
                Node("blue", "constant.color", new JsonObject
                {
                    ["r"] = 0,
                    ["g"] = 0,
                    ["b"] = 255
                }),
                Node("low", "output.segment-color", new JsonObject
                {
                    ["priority"] = 0,
                    ["blendMode"] = "override"
                }),
                Node("high", "output.segment-color", new JsonObject
                {
                    ["priority"] = 1,
                    ["blendMode"] = "add"
                })
            ],
            Connections =
            [
                Link("red", "color", "low", "color"),
                Link("blue", "color", "high", "color")
            ]
        };

        Render(settings, TimeSpan.Zero, InputSnapshot.Empty);

        AssertLeds(settings.Devices[0].Segments[0], 255, 0, 255, 255, 0, 255);
    }

    [Fact]
    public void BooleanInputMergeAllDrivesSelectColor()
    {
        var settings = CreateSettings(1);
        settings.Graph = new NodeGraph
        {
            Nodes =
            [
                Node("input", "input.bool", new JsonObject
                {
                    ["key"] = "left,right",
                    ["mergeMode"] = "all"
                }),
                Node("select", "logic.select-color", new JsonObject
                {
                    ["trueColor"] = new JsonObject
                    {
                        ["r"] = 9,
                        ["g"] = 8,
                        ["b"] = 7
                    },
                    ["falseColor"] = new JsonObject
                    {
                        ["r"] = 1,
                        ["g"] = 2,
                        ["b"] = 3
                    }
                }),
                Node("out", "output.segment-color")
            ],
            Connections =
            [
                Link("input", "value", "select", "condition"),
                Link("select", "color", "out", "color")
            ]
        };

        var snapshot = new InputSnapshot
        {
            BoolValues = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                ["left"] = true,
                ["right"] = false
            }
        };

        Render(settings, TimeSpan.Zero, snapshot);

        AssertLeds(settings.Devices[0].Segments[0], 1, 2, 3);
    }

    [Fact]
    public void PulseAndEnvelopeKeepStateAcrossFrames()
    {
        var settings = CreateSettings(1, 1);
        settings.Graph = new NodeGraph
        {
            Nodes =
            [
                Node("trigger", "input.bool", new JsonObject
                {
                    ["key"] = "hit"
                }),
                Node("pulse", "time.pulse", new JsonObject
                {
                    ["duration"] = 1
                }),
                Node("pulseBrightness", "color.brightness", new JsonObject
                {
                    ["color"] = new JsonObject
                    {
                        ["r"] = 100,
                        ["g"] = 0,
                        ["b"] = 0
                    }
                }),
                Node("pulseOut", "output.segment-color", new JsonObject
                {
                    ["segmentIds"] = "segment-0"
                }),
                Node("envelope", "time.envelope", new JsonObject
                {
                    ["release"] = 1
                }),
                Node("envelopeBrightness", "color.brightness", new JsonObject
                {
                    ["color"] = new JsonObject
                    {
                        ["r"] = 0,
                        ["g"] = 100,
                        ["b"] = 0
                    }
                }),
                Node("envelopeOut", "output.segment-color", new JsonObject
                {
                    ["segmentIds"] = "segment-1"
                })
            ],
            Connections =
            [
                Link("trigger", "value", "pulse", "trigger"),
                Link("pulse", "value", "pulseBrightness", "factor"),
                Link("pulseBrightness", "color", "pulseOut", "color"),
                Link("trigger", "value", "envelope", "trigger"),
                Link("envelope", "value", "envelopeBrightness", "factor"),
                Link("envelopeBrightness", "color", "envelopeOut", "color")
            ]
        };

        var prepared = Prepare(settings);

        Render(settings, prepared, TimeSpan.Zero, Snapshot(false));
        AssertLeds(settings.Devices[0].Segments[0], 0, 0, 0);
        AssertLeds(settings.Devices[0].Segments[1], 0, 0, 0);

        Render(settings, prepared, TimeSpan.FromSeconds(0.25), Snapshot(true));
        AssertLeds(settings.Devices[0].Segments[0], 100, 0, 0);
        AssertLeds(settings.Devices[0].Segments[1], 0, 100, 0);

        Render(settings, prepared, TimeSpan.FromSeconds(0.5), Snapshot(true));
        AssertLeds(settings.Devices[0].Segments[0], 75, 0, 0);
        AssertLeds(settings.Devices[0].Segments[1], 0, 100, 0);

        Render(settings, prepared, TimeSpan.FromSeconds(0.75), Snapshot(false));
        AssertLeds(settings.Devices[0].Segments[0], 50, 0, 0);
        AssertLeds(settings.Devices[0].Segments[1], 0, 100, 0);

        Render(settings, prepared, TimeSpan.FromSeconds(1.25), Snapshot(false));
        AssertLeds(settings.Devices[0].Segments[0], 0, 0, 0);
        AssertLeds(settings.Devices[0].Segments[1], 0, 50, 0);
    }

    private static PreparedGraph Prepare(Settings settings)
    {
        var catalog = new DefaultNodeTypeCatalog();
        var evaluator = new GraphRuntimeEvaluator(new NodeGraphCompiler(catalog), catalog);
        return evaluator.Prepare(settings) ?? throw new InvalidOperationException("Graph did not prepare.");
    }

    private static void Render(Settings settings, TimeSpan totalElapsed, InputSnapshot inputSnapshot)
    {
        var prepared = Prepare(settings);
        Render(settings, prepared, totalElapsed, inputSnapshot);
    }

    private static void Render(
        Settings settings,
        PreparedGraph prepared,
        TimeSpan totalElapsed,
        InputSnapshot inputSnapshot)
    {
        var catalog = new DefaultNodeTypeCatalog();
        var evaluator = new GraphRuntimeEvaluator(new NodeGraphCompiler(catalog), catalog);
        evaluator.Render(settings, prepared, new LightingFrameContext(0, totalElapsed, TimeSpan.FromSeconds(1.0 / 60.0), inputSnapshot));
    }

    private static Settings CreateSettings(params int[] segmentLengths)
    {
        var segments = segmentLengths
            .Select((length, index) => new Segment($"Segment {index}", length)
            {
                Id = $"segment-{index}"
            })
            .ToList();

        return new Settings
        {
            Devices =
            [
                new Device
                {
                    Id = "device",
                    Name = "Device",
                    Ip = "127.0.0.1",
                    Segments = segments
                }
            ]
        };
    }

    private static InputSnapshot Snapshot(bool hit)
    {
        return new InputSnapshot
        {
            BoolValues = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                ["hit"] = hit
            }
        };
    }

    private static NodeInstance Node(string id, string typeId, JsonObject? properties = null)
    {
        return new NodeInstance
        {
            Id = id,
            TypeId = typeId,
            Properties = properties ?? []
        };
    }

    private static Connection Link(
        string sourceNodeId,
        string sourcePortId,
        string targetNodeId,
        string targetPortId)
    {
        return new Connection
        {
            Id = $"{sourceNodeId}-{sourcePortId}-{targetNodeId}-{targetPortId}",
            SourceNodeId = sourceNodeId,
            SourcePortId = sourcePortId,
            TargetNodeId = targetNodeId,
            TargetPortId = targetPortId
        };
    }

    private static void AssertLeds(Segment segment, params byte[] expected)
    {
        var actual = new byte[segment.Leds.Length * 3];
        for (var i = 0; i < segment.Leds.Length; i++)
        {
            actual[(i * 3) + 0] = segment.Leds[i].R;
            actual[(i * 3) + 1] = segment.Leds[i].G;
            actual[(i * 3) + 2] = segment.Leds[i].B;
        }

        Assert.Equal(expected, actual);
    }
}
