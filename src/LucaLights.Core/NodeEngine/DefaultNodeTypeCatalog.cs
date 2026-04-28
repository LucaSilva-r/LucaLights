using System.Text.Json.Nodes;

namespace LucaLights.Core.NodeEngine;

public sealed class DefaultNodeTypeCatalog : INodeTypeCatalog
{
    private readonly IReadOnlyList<NodeTypeDefinition> _nodeTypes;
    private readonly Dictionary<string, NodeTypeDefinition> _nodeTypesById;

    public DefaultNodeTypeCatalog()
    {
        _nodeTypes =
        [
            CommentNote(),
            RerouteBool(),
            RerouteFloat(),
            RerouteColor(),
            ConstantColor(),
            ConstantFloat(),
            ConstantBool(),
            GraphBoolInput(),
            GraphFloatInput(),
            GraphColorInput(),
            SelectColor(),
            SelectFloat(),
            LogicNot(),
            LogicAnd(),
            LogicOr(),
            LogicCompare(),
            MixColor(),
            MathAdd(),
            MathMultiply(),
            MathClamp(),
            MathRemap(),
            MathWrap(),
            MathPingPong(),
            MathModulo(),
            MathAbs(),
            MathStep(),
            MathSmoothStep(),
            MathSin(),
            MathCos(),
            MathTan(),
            MathRandom(),
            MathPerlinNoise2D(),
            MathValueNoise2D(),
            ColorBrightness(),
            ColorHsv(),
            ColorToHsv(),
            ColorGradient(),
            TimeElapsed(),
            TimeOscillator(),
            TimePulse(),
            TimeEnvelope(),
            TimeBlink(),
            PixelInfo(),
            SegmentColorOutput()
        ];

        _nodeTypesById = _nodeTypes.ToDictionary(
            nodeType => nodeType.TypeId,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<NodeTypeDefinition> GetNodeTypes()
    {
        return _nodeTypes;
    }

    public bool TryGetNodeType(string typeId, out NodeTypeDefinition? nodeType)
    {
        return _nodeTypesById.TryGetValue(typeId, out nodeType);
    }

    private static NodeTypeDefinition CommentNote()
    {
        return new NodeTypeDefinition(
            "annotation.comment",
            "Comment",
            "Annotations",
            "Adds a visual note to the graph without affecting evaluation.",
            [],
            [],
            [
                Property("title", "Title", NodeValueType.String, "Short heading shown in the node header.", "Comment"),
                Property("body", "Comment", NodeValueType.String, "Long-form note for documenting intent, TODOs, or context.", string.Empty)
            ]);
    }

    private static NodeTypeDefinition RerouteBool()
    {
        return Reroute(
            "reroute.bool",
            "Reroute Boolean",
            NodeValueType.Bool,
            "Routes a boolean connection through a movable waypoint to help tidy the graph.");
    }

    private static NodeTypeDefinition RerouteFloat()
    {
        return Reroute(
            "reroute.float",
            "Reroute Number",
            NodeValueType.Float,
            "Routes a number connection through a movable waypoint to help tidy the graph.");
    }

    private static NodeTypeDefinition RerouteColor()
    {
        return Reroute(
            "reroute.color",
            "Reroute Color",
            NodeValueType.Color,
            "Routes a color connection through a movable waypoint to help tidy the graph.");
    }

    private static NodeTypeDefinition ConstantColor()
    {
        return new NodeTypeDefinition(
            "constant.color",
            "Color",
            "Constants",
            "Outputs a fixed RGB color.",
            [],
            [Output("color", "Color", NodeValueType.Color, "The configured color.")],
            [
                Property("r", "Red", NodeValueType.Float, "Red channel.", 255, 0, 255),
                Property("g", "Green", NodeValueType.Float, "Green channel.", 255, 0, 255),
                Property("b", "Blue", NodeValueType.Float, "Blue channel.", 255, 0, 255)
            ]);
    }

    private static NodeTypeDefinition ConstantFloat()
    {
        return new NodeTypeDefinition(
            "constant.float",
            "Number",
            "Constants",
            "Outputs a fixed floating point value.",
            [],
            [Output("value", "Value", NodeValueType.Float, "The configured value.")],
            [Property("value", "Value", NodeValueType.Float, "The configured value.", 0)]);
    }

    private static NodeTypeDefinition ConstantBool()
    {
        return new NodeTypeDefinition(
            "constant.bool",
            "Boolean",
            "Constants",
            "Outputs a fixed boolean value.",
            [],
            [Output("value", "Value", NodeValueType.Bool, "The configured value.")],
            [Property("value", "Value", NodeValueType.Bool, "The configured value.", false)]);
    }

    private static NodeTypeDefinition GraphBoolInput()
    {
        return new NodeTypeDefinition(
            "input.bool",
            "Boolean Input",
            "Graph Inputs",
            "Reads one or more boolean input channels and merges them into a single boolean value.",
            [],
            [Output("value", "Value", NodeValueType.Bool, "The resolved input value.")],
            [
                Property("mergeMode", "Merge", NodeValueType.String, "How multiple selected channels are merged.", "any"),
                Property("key", "Channels", NodeValueType.String, "Comma-separated input channel keys.", string.Empty)
            ]);
    }

    private static NodeTypeDefinition GraphFloatInput()
    {
        return new NodeTypeDefinition(
            "input.float",
            "Number Input",
            "Graph Inputs",
            "Reads one or more numeric input channels and merges them into a single value.",
            [],
            [Output("value", "Value", NodeValueType.Float, "The resolved input value.")],
            [
                Property("mergeMode", "Merge", NodeValueType.String, "How multiple selected channels are merged.", "max"),
                Property("key", "Channels", NodeValueType.String, "Comma-separated input channel keys.", string.Empty)
            ]);
    }

    private static NodeTypeDefinition GraphColorInput()
    {
        return new NodeTypeDefinition(
            "input.color",
            "Color Input",
            "Graph Inputs",
            "Reads one or more color input channels and merges them into a single color value.",
            [],
            [Output("value", "Value", NodeValueType.Color, "The resolved input value.")],
            [
                Property("mergeMode", "Merge", NodeValueType.String, "How multiple selected channels are merged.", "average"),
                Property("key", "Channels", NodeValueType.String, "Comma-separated input channel keys.", string.Empty)
            ]);
    }

    private static NodeTypeDefinition SelectColor()
    {
        return new NodeTypeDefinition(
            "logic.select-color",
            "Select Color",
            "Logic",
            "Chooses between two colors using a boolean condition.",
            [
                Input("condition", "Condition", NodeValueType.Bool, "When true, outputs the true color."),
                Input("trueColor", "True", NodeValueType.Color, "Color to output when the condition is true."),
                Input("falseColor", "False", NodeValueType.Color, "Color to output when the condition is false.")
            ],
            [Output("color", "Color", NodeValueType.Color, "The selected color.")],
            [
                Property("condition", "Condition", NodeValueType.Bool, "Fallback condition when no condition input is connected.", false),
                ColorProperty("trueColor", "True", "Fallback color when the condition is true.", 255, 255, 255),
                ColorProperty("falseColor", "False", "Fallback color when the condition is false.", 0, 0, 0)
            ]);
    }

    private static NodeTypeDefinition MixColor()
    {
        return new NodeTypeDefinition(
            "logic.mix-color",
            "Mix Color",
            "Color",
            "Blends between two colors using a selectable blend mode and factor from 0 to 1.",
            [
                Input("a", "A", NodeValueType.Color, "The first color input."),
                Input("b", "B", NodeValueType.Color, "The second color input."),
                Input("factor", "Factor", NodeValueType.Float, "Blend amount from 0 to 1.")
            ],
            [Output("color", "Color", NodeValueType.Color, "The mixed color.")],
            [
                ColorProperty("a", "A", "Fallback color for A.", 0, 0, 0),
                ColorProperty("b", "B", "Fallback color for B.", 255, 255, 255),
                Property("mode", "Mode", NodeValueType.String, "Blend mode used to combine A and B.", "mix"),
                Property("factor", "Factor", NodeValueType.Float, "Fallback blend amount from 0 to 1 when no factor input is connected.", 0.5, 0, 1)
            ]);
    }

    private static NodeTypeDefinition SelectFloat()
    {
        return new NodeTypeDefinition(
            "logic.select-float",
            "Select Number",
            "Logic",
            "Chooses between two numbers using a boolean condition.",
            [
                Input("condition", "Condition", NodeValueType.Bool, "When true, outputs the true value."),
                Input("true", "True", NodeValueType.Float, "Value to output when the condition is true."),
                Input("false", "False", NodeValueType.Float, "Value to output when the condition is false.")
            ],
            [Output("value", "Value", NodeValueType.Float, "The selected value.")],
            [
                Property("condition", "Condition", NodeValueType.Bool, "Fallback condition when no condition input is connected.", false),
                Property("true", "True", NodeValueType.Float, "Fallback value when condition is true.", 1),
                Property("false", "False", NodeValueType.Float, "Fallback value when condition is false.", 0)
            ]);
    }

    private static NodeTypeDefinition LogicNot()
    {
        return new NodeTypeDefinition(
            "logic.not",
            "Not",
            "Logic",
            "Inverts a boolean value.",
            [Input("value", "Value", NodeValueType.Bool, "The boolean to invert.")],
            [Output("value", "Value", NodeValueType.Bool, "The inverted boolean.")],
            [Property("value", "Value", NodeValueType.Bool, "Fallback input value.", false)]);
    }

    private static NodeTypeDefinition LogicAnd()
    {
        return new NodeTypeDefinition(
            "logic.and",
            "And",
            "Logic",
            "Outputs true when both inputs are true.",
            [
                Input("a", "A", NodeValueType.Bool, "First boolean input."),
                Input("b", "B", NodeValueType.Bool, "Second boolean input.")
            ],
            [Output("value", "Value", NodeValueType.Bool, "True when both inputs are true.")],
            [
                Property("a", "A", NodeValueType.Bool, "Fallback value for A.", false),
                Property("b", "B", NodeValueType.Bool, "Fallback value for B.", false)
            ]);
    }

    private static NodeTypeDefinition LogicOr()
    {
        return new NodeTypeDefinition(
            "logic.or",
            "Or",
            "Logic",
            "Outputs true when either input is true.",
            [
                Input("a", "A", NodeValueType.Bool, "First boolean input."),
                Input("b", "B", NodeValueType.Bool, "Second boolean input.")
            ],
            [Output("value", "Value", NodeValueType.Bool, "True when either input is true.")],
            [
                Property("a", "A", NodeValueType.Bool, "Fallback value for A.", false),
                Property("b", "B", NodeValueType.Bool, "Fallback value for B.", false)
            ]);
    }

    private static NodeTypeDefinition LogicCompare()
    {
        return new NodeTypeDefinition(
            "logic.compare",
            "Compare",
            "Logic",
            "Compares two numbers and outputs a boolean result.",
            [
                Input("a", "A", NodeValueType.Float, "Left-hand value."),
                Input("b", "B", NodeValueType.Float, "Right-hand value.")
            ],
            [Output("value", "Value", NodeValueType.Bool, "The comparison result.")],
            [
                Property("a", "A", NodeValueType.Float, "Fallback left-hand value.", 0),
                Property("b", "B", NodeValueType.Float, "Fallback right-hand value.", 0.5),
                Property("mode", "Mode", NodeValueType.String, "Comparison mode: greater, less, or equal.", "greater")
            ]);
    }

    private static NodeTypeDefinition MathAdd()
    {
        return new NodeTypeDefinition(
            "math.add",
            "Add",
            "Math",
            "Adds two numbers together.",
            [
                Input("a", "A", NodeValueType.Float, "First number."),
                Input("b", "B", NodeValueType.Float, "Second number.")
            ],
            [Output("value", "Value", NodeValueType.Float, "The sum of A and B.")],
            [
                Property("a", "A", NodeValueType.Float, "Fallback value for A.", 0),
                Property("b", "B", NodeValueType.Float, "Fallback value for B.", 0)
            ]);
    }

    private static NodeTypeDefinition MathMultiply()
    {
        return new NodeTypeDefinition(
            "math.multiply",
            "Multiply",
            "Math",
            "Multiplies two numbers together.",
            [
                Input("a", "A", NodeValueType.Float, "First number."),
                Input("b", "B", NodeValueType.Float, "Second number.")
            ],
            [Output("value", "Value", NodeValueType.Float, "The product of A and B.")],
            [
                Property("a", "A", NodeValueType.Float, "Fallback value for A.", 1),
                Property("b", "B", NodeValueType.Float, "Fallback value for B.", 1)
            ]);
    }

    private static NodeTypeDefinition MathClamp()
    {
        return new NodeTypeDefinition(
            "math.clamp",
            "Clamp",
            "Math",
            "Constrains a value to a range.",
            [
                Input("value", "Value", NodeValueType.Float, "The value to clamp."),
                Input("min", "Min", NodeValueType.Float, "Minimum bound."),
                Input("max", "Max", NodeValueType.Float, "Maximum bound.")
            ],
            [Output("value", "Value", NodeValueType.Float, "The clamped value.")],
            [
                Property("value", "Value", NodeValueType.Float, "Fallback input value.", 0),
                Property("min", "Min", NodeValueType.Float, "Fallback minimum.", 0),
                Property("max", "Max", NodeValueType.Float, "Fallback maximum.", 1)
            ]);
    }

    private static NodeTypeDefinition MathRemap()
    {
        return new NodeTypeDefinition(
            "math.remap",
            "Remap",
            "Math",
            "Maps a value from one range to another.",
            [Input("value", "Value", NodeValueType.Float, "The value to remap.")],
            [Output("value", "Value", NodeValueType.Float, "The remapped value.")],
            [
                Property("value", "Value", NodeValueType.Float, "Fallback input value.", 0),
                Property("inMin", "In Min", NodeValueType.Float, "Input range minimum.", 0),
                Property("inMax", "In Max", NodeValueType.Float, "Input range maximum.", 1),
                Property("outMin", "Out Min", NodeValueType.Float, "Output range minimum.", 0),
                Property("outMax", "Out Max", NodeValueType.Float, "Output range maximum.", 1)
            ]);
    }

    private static NodeTypeDefinition MathWrap()
    {
        return new NodeTypeDefinition(
            "math.wrap",
            "Wrap",
            "Math",
            "Repeats a value inside a range by wrapping it back to the minimum after it passes the maximum.",
            [
                Input("value", "Value", NodeValueType.Float, "The value to wrap."),
                Input("min", "Min", NodeValueType.Float, "Lower bound of the wrap range."),
                Input("max", "Max", NodeValueType.Float, "Upper bound of the wrap range.")
            ],
            [Output("value", "Value", NodeValueType.Float, "The wrapped value.")],
            [
                Property("value", "Value", NodeValueType.Float, "Fallback input value.", 0),
                Property("min", "Min", NodeValueType.Float, "Fallback minimum.", 0),
                Property("max", "Max", NodeValueType.Float, "Fallback maximum.", 1)
            ]);
    }

    private static NodeTypeDefinition MathPingPong()
    {
        return new NodeTypeDefinition(
            "math.ping-pong",
            "Ping-Pong",
            "Math",
            "Repeats a value by bouncing it back and forth between 0 and the scale.",
            [
                Input("value", "Value", NodeValueType.Float, "The value to ping-pong."),
                Input("scale", "Scale", NodeValueType.Float, "Maximum bounce distance.")
            ],
            [Output("value", "Value", NodeValueType.Float, "The ping-ponged value.")],
            [
                Property("value", "Value", NodeValueType.Float, "Fallback input value.", 0),
                Property("scale", "Scale", NodeValueType.Float, "Fallback bounce scale.", 1)
            ]);
    }

    private static NodeTypeDefinition MathModulo()
    {
        return new NodeTypeDefinition(
            "math.modulo",
            "Modulo",
            "Math",
            "Computes the remainder of dividing a value by a divisor.",
            [
                Input("value", "Value", NodeValueType.Float, "The dividend."),
                Input("divisor", "Divisor", NodeValueType.Float, "The divisor.")
            ],
            [Output("value", "Value", NodeValueType.Float, "The remainder.")],
            [
                Property("value", "Value", NodeValueType.Float, "Fallback dividend.", 0),
                Property("divisor", "Divisor", NodeValueType.Float, "Fallback divisor.", 1)
            ]);
    }

    private static NodeTypeDefinition MathAbs()
    {
        return new NodeTypeDefinition(
            "math.abs",
            "Absolute",
            "Math",
            "Outputs the absolute value of a number.",
            [Input("value", "Value", NodeValueType.Float, "The input value.")],
            [Output("value", "Value", NodeValueType.Float, "The absolute value.")],
            [Property("value", "Value", NodeValueType.Float, "Fallback input value.", 0)]);
    }

    private static NodeTypeDefinition MathStep()
    {
        return new NodeTypeDefinition(
            "math.step",
            "Step",
            "Math",
            "Outputs 0 below the edge and 1 at or above the edge.",
            [
                Input("value", "Value", NodeValueType.Float, "The input value."),
                Input("edge", "Edge", NodeValueType.Float, "The threshold edge.")
            ],
            [Output("value", "Value", NodeValueType.Float, "0 or 1.")],
            [
                Property("value", "Value", NodeValueType.Float, "Fallback input value.", 0),
                Property("edge", "Edge", NodeValueType.Float, "Fallback edge threshold.", 0.5, 0, 1)
            ]);
    }

    private static NodeTypeDefinition MathSmoothStep()
    {
        return new NodeTypeDefinition(
            "math.smooth-step",
            "Smooth Step",
            "Math",
            "Smooth hermite interpolation between 0 and 1 over an edge range.",
            [Input("value", "Value", NodeValueType.Float, "The input value.")],
            [Output("value", "Value", NodeValueType.Float, "Smoothly interpolated 0 to 1.")],
            [
                Property("value", "Value", NodeValueType.Float, "Fallback input value.", 0),
                Property("edge0", "Edge 0", NodeValueType.Float, "Lower edge of the transition.", 0, 0, 1),
                Property("edge1", "Edge 1", NodeValueType.Float, "Upper edge of the transition.", 1, 0, 1)
            ]);
    }

    private static NodeTypeDefinition MathSin()
    {
        return new NodeTypeDefinition(
            "math.sin",
            "Sine",
            "Math",
            "Computes the sine of the input value (radians).",
            [Input("value", "Value", NodeValueType.Float, "Input in radians.")],
            [Output("value", "Value", NodeValueType.Float, "Sine result (-1 to 1).")],
            [Property("value", "Value", NodeValueType.Float, "Fallback input value (radians).", 0)]);
    }

    private static NodeTypeDefinition MathCos()
    {
        return new NodeTypeDefinition(
            "math.cos",
            "Cosine",
            "Math",
            "Computes the cosine of the input value (radians).",
            [Input("value", "Value", NodeValueType.Float, "Input in radians.")],
            [Output("value", "Value", NodeValueType.Float, "Cosine result (-1 to 1).")],
            [Property("value", "Value", NodeValueType.Float, "Fallback input value (radians).", 0)]);
    }

    private static NodeTypeDefinition MathTan()
    {
        return new NodeTypeDefinition(
            "math.tan",
            "Tangent",
            "Math",
            "Computes the tangent of the input value (radians).",
            [Input("value", "Value", NodeValueType.Float, "Input in radians.")],
            [Output("value", "Value", NodeValueType.Float, "Tangent result.")],
            [Property("value", "Value", NodeValueType.Float, "Fallback input value (radians).", 0)]);
    }

    private static NodeTypeDefinition MathRandom()
    {
        return new NodeTypeDefinition(
            "math.random",
            "Random Number",
            "Noise",
            "Outputs a fast pseudo-random number between the configured minimum and maximum.",
            [
                Input("min", "Min", NodeValueType.Float, "Lower bound of the random range."),
                Input("max", "Max", NodeValueType.Float, "Upper bound of the random range."),
                Input("seed", "Seed", NodeValueType.Float, "Seed value for the random sequence.")
            ],
            [Output("value", "Value", NodeValueType.Float, "Random value in the configured range.")],
            [
                Property("min", "Min", NodeValueType.Float, "Fallback minimum.", 0),
                Property("max", "Max", NodeValueType.Float, "Fallback maximum.", 1),
                Property("seed", "Seed", NodeValueType.Float, "Fallback random seed.", 0)
            ]);
    }

    private static NodeTypeDefinition MathPerlinNoise2D()
    {
        return new NodeTypeDefinition(
            "math.perlin-2d",
            "Perlin Noise 2D",
            "Noise",
            "Samples smooth two-axis Perlin noise and outputs a value from 0 to 1.",
            [
                Input("x", "X", NodeValueType.Float, "Horizontal sample coordinate."),
                Input("y", "Y", NodeValueType.Float, "Vertical sample coordinate.")
            ],
            [Output("value", "Value", NodeValueType.Float, "Noise value from 0 to 1.")],
            [
                Property("x", "X", NodeValueType.Float, "Fallback horizontal coordinate.", 0),
                Property("y", "Y", NodeValueType.Float, "Fallback vertical coordinate.", 0)
            ]);
    }

    private static NodeTypeDefinition MathValueNoise2D()
    {
        return new NodeTypeDefinition(
            "math.value-noise-2d",
            "Value Noise 2D",
            "Noise",
            "Samples cheap smooth two-axis value noise and outputs a value from 0 to 1.",
            [
                Input("x", "X", NodeValueType.Float, "Horizontal sample coordinate."),
                Input("y", "Y", NodeValueType.Float, "Vertical sample coordinate.")
            ],
            [Output("value", "Value", NodeValueType.Float, "Noise value from 0 to 1.")],
            [
                Property("x", "X", NodeValueType.Float, "Fallback horizontal coordinate.", 0),
                Property("y", "Y", NodeValueType.Float, "Fallback vertical coordinate.", 0)
            ]);
    }

    private static NodeTypeDefinition ColorBrightness()
    {
        return new NodeTypeDefinition(
            "color.brightness",
            "Brightness",
            "Color",
            "Scales the brightness of a color by a factor.",
            [
                Input("color", "Color", NodeValueType.Color, "The color to adjust."),
                Input("factor", "Factor", NodeValueType.Float, "Brightness multiplier.")
            ],
            [Output("color", "Color", NodeValueType.Color, "The brightness-adjusted color.")],
            [
                ColorProperty("color", "Color", "Fallback input color.", 255, 255, 255),
                Property("factor", "Factor", NodeValueType.Float, "Fallback brightness factor.", 1, 0, 2)
            ]);
    }

    private static NodeTypeDefinition ColorHsv()
    {
        return new NodeTypeDefinition(
            "color.hsv",
            "HSV to Color",
            "Color",
            "Creates a color from hue, saturation, and value components.",
            [
                Input("hue", "Hue", NodeValueType.Float, "Hue in degrees (0-360)."),
                Input("saturation", "Saturation", NodeValueType.Float, "Saturation (0-1)."),
                Input("brightness", "Brightness", NodeValueType.Float, "Value/brightness (0-1).")
            ],
            [Output("color", "Color", NodeValueType.Color, "The resulting RGB color.")],
            [
                Property("hue", "Hue", NodeValueType.Float, "Fallback hue in degrees.", 0, 0, 360),
                Property("saturation", "Saturation", NodeValueType.Float, "Fallback saturation.", 1, 0, 1),
                Property("brightness", "Brightness", NodeValueType.Float, "Fallback brightness.", 1, 0, 1)
            ]);
    }

    private static NodeTypeDefinition ColorToHsv()
    {
        return new NodeTypeDefinition(
            "color.to-hsv",
            "Color to HSV",
            "Color",
            "Decomposes an RGB color into hue, saturation, and value components. Pair with 'HSV to Color' to manipulate individual channels (e.g. lock saturation to 1 for always-vivid output).",
            [Input("color", "Color", NodeValueType.Color, "The color to decompose.")],
            [
                Output("hue", "Hue", NodeValueType.Float, "Hue in degrees (0-360)."),
                Output("saturation", "Saturation", NodeValueType.Float, "Saturation (0-1)."),
                Output("brightness", "Brightness", NodeValueType.Float, "Value/brightness (0-1).")
            ],
            [ColorProperty("color", "Color", "Fallback color when no input is connected.", 255, 255, 255)]);
    }

    private static NodeTypeDefinition ColorGradient()
    {
        return new NodeTypeDefinition(
            "color.gradient",
            "Gradient",
            "Color",
            "Samples a multi-stop color gradient at a given factor (0-1). Stops are defined as a JSON array.",
            [Input("factor", "Factor", NodeValueType.Float, "Position to sample the gradient (0-1).")],
            [Output("color", "Color", NodeValueType.Color, "The sampled gradient color.")],
            [
                Property("factor", "Factor", NodeValueType.Float, "Fallback sample position.", 0.5, 0, 1),
                Property("stops", "Stops", NodeValueType.String,
                    "JSON gradient stops: [{\"p\":0,\"r\":0,\"g\":0,\"b\":0},{\"p\":1,\"r\":255,\"g\":255,\"b\":255}]",
                    "[{\"p\":0,\"r\":0,\"g\":0,\"b\":0},{\"p\":1,\"r\":255,\"g\":255,\"b\":255}]"),
                Property("interpolation", "Interpolation", NodeValueType.String, "Interpolation mode: linear or constant.", "linear")
            ]);
    }

    private static NodeTypeDefinition TimeElapsed()
    {
        return new NodeTypeDefinition(
            "time.elapsed",
            "Elapsed Time",
            "Time",
            "Outputs the total elapsed time in seconds since the engine started.",
            [],
            [Output("seconds", "Seconds", NodeValueType.Float, "Total elapsed seconds.")],
            []);
    }

    private static NodeTypeDefinition TimeOscillator()
    {
        return new NodeTypeDefinition(
            "time.oscillator",
            "Oscillator",
            "Time",
            "Outputs a repeating 0-1 wave at a configurable speed and waveform.",
            [Input("speed", "Speed", NodeValueType.Float, "Cycles per second multiplier.")],
            [Output("value", "Value", NodeValueType.Float, "The oscillator value between 0 and 1.")],
            [
                Property("speed", "Speed", NodeValueType.Float, "Fallback cycles per second.", 1),
                Property("waveform", "Waveform", NodeValueType.String, "Wave shape: sine, square, triangle, or sawtooth.", "sine")
            ]);
    }

    private static NodeTypeDefinition TimePulse()
    {
        return new NodeTypeDefinition(
            "time.pulse",
            "Pulse",
            "Time",
            "Outputs a 1-to-0 fade when triggered by a boolean edge.",
            [
                Input("trigger", "Trigger", NodeValueType.Bool, "Edge transition starts a new pulse."),
                Input("duration", "Duration", NodeValueType.Float, "Pulse duration in seconds.")
            ],
            [Output("value", "Value", NodeValueType.Float, "The pulse value from 1 to 0.")],
            [
                Property("trigger", "Trigger", NodeValueType.Bool, "Fallback trigger state.", false),
                Property("duration", "Duration", NodeValueType.Float, "Fallback pulse duration in seconds.", 0.5, 0, 10),
                Property("edge", "Edge", NodeValueType.String, "Which edge triggers the pulse: rising or falling.", "rising")
            ]);
    }

    private static NodeTypeDefinition TimeEnvelope()
    {
        return new NodeTypeDefinition(
            "time.envelope",
            "Envelope",
            "Time",
            "Holds at 1 while the trigger is true, then fades to 0 over a configurable release duration.",
            [
                Input("trigger", "Trigger", NodeValueType.Bool, "Hold at 1 while true, fade on release."),
                Input("release", "Release", NodeValueType.Float, "Fade duration in seconds after release.")
            ],
            [Output("value", "Value", NodeValueType.Float, "1 while held, fading to 0 after release.")],
            [
                Property("trigger", "Trigger", NodeValueType.Bool, "Fallback trigger state.", false),
                Property("release", "Release", NodeValueType.Float, "Fallback release duration in seconds.", 0.5, 0, 10)
            ]);
    }

    private static NodeTypeDefinition TimeBlink()
    {
        return new NodeTypeDefinition(
            "time.blink",
            "Blink",
            "Time",
            "Outputs a toggling boolean that switches between true and false at configurable on and off durations.",
            [
                Input("onTime", "On Time", NodeValueType.Float, "Duration the output stays true (on), in seconds."),
                Input("offTime", "Off Time", NodeValueType.Float, "Duration the output stays false (off), in seconds.")
            ],
            [Output("value", "Value", NodeValueType.Bool, "The blinking boolean output.")],
            [
                Property("onTime", "On Time", NodeValueType.Float, "Fallback duration the output stays true (on), in seconds.", 0.5f, 0.01f, 60f),
                Property("offTime", "Off Time", NodeValueType.Float, "Fallback duration the output stays false (off), in seconds.", 0.5f, 0.01f, 60f)
            ]);
    }

    private static NodeTypeDefinition PixelInfo()
    {
        return new NodeTypeDefinition(
            "pixel.info",
            "Pixel Info",
            "Segment",
            "Outputs current pixel layout coordinates when evaluated per-pixel.",
            [],
            [
                Output("index", "Segment Pixel", NodeValueType.Float, "Zero-based pixel index within the segment."),
                Output("length", "Segment Count", NodeValueType.Float, "Total number of LEDs in the segment."),
                Output("normalized", "Segment Position", NodeValueType.Float, "Pixel position normalized within the segment."),
                Output("deviceIndex", "Device #", NodeValueType.Float, "Zero-based index of the device containing the current pixel."),
                Output("segmentIndex", "Segment #", NodeValueType.Float, "Zero-based index of the segment within its device."),
                Output("devicePixelIndex", "Device Pixel", NodeValueType.Float, "Zero-based pixel index within the device, spanning its segments."),
                Output("deviceLength", "Device Count", NodeValueType.Float, "Total number of LEDs in the current device."),
                Output("deviceNormalized", "Device Position", NodeValueType.Float, "Pixel position normalized across the current device."),
                Output("globalIndex", "Global Pixel", NodeValueType.Float, "Zero-based pixel index across all rendered target segments."),
                Output("globalLength", "Global Count", NodeValueType.Float, "Total number of LEDs across all rendered target segments."),
                Output("globalNormalized", "Global Position", NodeValueType.Float, "Pixel position normalized across all rendered target segments."),
                Output("layoutX", "Layout X", NodeValueType.Float, "Authored LED X coordinate normalized from 0 to 1."),
                Output("layoutY", "Layout Y", NodeValueType.Float, "Authored LED Y coordinate normalized from 0 to 1.")
            ],
            []);
    }

    private static NodeTypeDefinition SegmentColorOutput()
    {
        return new NodeTypeDefinition(
            "output.segment-color",
            "Output",
            "Outputs",
            "Writes a color to the selected output segments. Outputs are applied in priority order, then blended into the current LED state.",
            [
                Input("color", "Color", NodeValueType.Color, "Color to write to the target segments."),
                Input("active", "Active", NodeValueType.Bool, "When false, this output is skipped entirely.")
            ],
            [],
            [
                ColorProperty("color", "Color", "Fallback output color.", 255, 255, 255),
                Property("active", "Active", NodeValueType.Bool, "Fallback enabled state when no active input is connected.", true),
                Property("blendMode", "Blend", NodeValueType.String, "How this output combines with lower-priority output colors.", "override"),
                Property("priority", "Priority", NodeValueType.Float, "Higher priority outputs apply later and can override lower-priority outputs.", 0),
                Property("segmentIds", "Segments", NodeValueType.String, "Comma-separated segment IDs. Empty means all segments.", string.Empty)
            ]);
    }

    private static NodeTypeDefinition Reroute(
        string typeId,
        string displayName,
        NodeValueType valueType,
        string description)
    {
        return new NodeTypeDefinition(
            typeId,
            displayName,
            "Layout",
            description,
            [Input("value", "Value", valueType, "Incoming value routed through this waypoint.")],
            [Output("value", "Value", valueType, "Outgoing value routed through this waypoint.")],
            []);
    }

    private static NodePortDefinition Input(
        string id,
        string label,
        NodeValueType valueType,
        string description,
        bool allowMultipleConnections = false)
    {
        return new NodePortDefinition(
            id,
            label,
            valueType,
            NodePortDirection.Input,
            description,
            allowMultipleConnections);
    }

    private static NodePortDefinition Output(
        string id,
        string label,
        NodeValueType valueType,
        string description,
        bool allowMultipleConnections = true)
    {
        return new NodePortDefinition(
            id,
            label,
            valueType,
            NodePortDirection.Output,
            description,
            allowMultipleConnections);
    }

    private static NodePropertyDefinition Property(
        string key,
        string label,
        NodeValueType valueType,
        string description,
        JsonNode? defaultValue,
        float? minFloatValue = null,
        float? maxFloatValue = null)
    {
        return new NodePropertyDefinition(
            key,
            label,
            valueType,
            description,
            defaultValue,
            minFloatValue,
            maxFloatValue);
    }

    private static NodePropertyDefinition ColorProperty(
        string key,
        string label,
        string description,
        byte r,
        byte g,
        byte b)
    {
        return Property(
            key,
            label,
            NodeValueType.Color,
            description,
            new JsonObject
            {
                ["r"] = r,
                ["g"] = g,
                ["b"] = b
            });
    }
}
