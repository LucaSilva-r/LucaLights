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
            ConstantColor(),
            ConstantFloat(),
            ConstantBool(),
            GraphBoolInput(),
            GraphFloatInput(),
            GraphColorInput(),
            SelectColor(),
            MixColor(),
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
            "Reads a graph-level boolean input. Binding profiles decide which game channels feed it.",
            [],
            [Output("value", "Value", NodeValueType.Bool, "The resolved input value.")],
            [Property("key", "Input Key", NodeValueType.String, "Graph-level input key.", "input")]);
    }

    private static NodeTypeDefinition GraphFloatInput()
    {
        return new NodeTypeDefinition(
            "input.float",
            "Number Input",
            "Graph Inputs",
            "Reads a graph-level numeric input. Binding profiles decide which game channels feed it.",
            [],
            [Output("value", "Value", NodeValueType.Float, "The resolved input value.")],
            [Property("key", "Input Key", NodeValueType.String, "Graph-level input key.", "input")]);
    }

    private static NodeTypeDefinition GraphColorInput()
    {
        return new NodeTypeDefinition(
            "input.color",
            "Color Input",
            "Graph Inputs",
            "Reads a graph-level color input. Binding profiles decide which game channels feed it.",
            [],
            [Output("value", "Value", NodeValueType.Color, "The resolved input value.")],
            [Property("key", "Input Key", NodeValueType.String, "Graph-level input key.", "input")]);
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
            []);
    }

    private static NodeTypeDefinition MixColor()
    {
        return new NodeTypeDefinition(
            "logic.mix-color",
            "Mix Color",
            "Math",
            "Blends between two colors using a factor from 0 to 1.",
            [
                Input("a", "A", NodeValueType.Color, "The first color input."),
                Input("b", "B", NodeValueType.Color, "The second color input."),
                Input("factor", "Factor", NodeValueType.Float, "Blend amount from 0 to 1.")
            ],
            [Output("color", "Color", NodeValueType.Color, "The mixed color.")],
            [Property("factor", "Factor", NodeValueType.Float, "Fallback blend amount from 0 to 1 when no factor input is connected.", 0.5, 0, 1)]);
    }

    private static NodeTypeDefinition SegmentColorOutput()
    {
        return new NodeTypeDefinition(
            "output.segment-color",
            "Segment Color Output",
            "Outputs",
            "Writes a color to one or more target segments. Phase 2 rendering will interpret the target properties.",
            [Input("color", "Color", NodeValueType.Color, "Color to write to the target segments.")],
            [],
            [
                Property("deviceIds", "Device IDs", NodeValueType.String, "Comma-separated device IDs. Empty means all devices.", string.Empty),
                Property("segmentIds", "Segment IDs", NodeValueType.String, "Comma-separated segment IDs. Empty means all segments.", string.Empty),
                Property("groupIds", "Group IDs", NodeValueType.String, "Comma-separated group IDs. Empty means all groups.", string.Empty)
            ]);
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
}
