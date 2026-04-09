using LucaLights.Core.Models;

namespace LucaLights.Core.NodeEngine;

public sealed class NodeGraphCompiler
{
    private readonly INodeTypeCatalog _nodeTypeCatalog;

    public NodeGraphCompiler(INodeTypeCatalog nodeTypeCatalog)
    {
        _nodeTypeCatalog = nodeTypeCatalog;
    }

    public GraphValidationResult Validate(NodeGraph? graph)
    {
        return Compile(graph).Validation;
    }

    public CompiledNodeGraph Compile(NodeGraph? graph)
    {
        if (graph is null)
        {
            return new CompiledNodeGraph(
                new NodeGraph(),
                [],
                new GraphValidationResult(
                    false,
                    [Error("graph.null", "Graph body is required.")]));
        }

        NormalizeGraph(graph);

        var diagnostics = new List<GraphDiagnostic>();
        var nodesById = BuildNodeIndex(graph, diagnostics);
        var nodeTypesByNodeId = ResolveNodeTypes(nodesById, diagnostics);
        ValidateConnections(graph, nodesById, nodeTypesByNodeId, diagnostics);
        var evaluationOrder = BuildEvaluationOrder(graph, nodesById, diagnostics);
        ValidateOutputs(graph, nodeTypesByNodeId, diagnostics);

        var isValid = diagnostics.All(diagnostic => diagnostic.Severity != GraphDiagnosticSeverity.Error);
        return new CompiledNodeGraph(
            graph,
            isValid ? evaluationOrder : [],
            new GraphValidationResult(isValid, diagnostics));
    }

    public static void NormalizeGraph(NodeGraph graph)
    {
        graph.Nodes ??= [];
        graph.Connections ??= [];

        foreach (var node in graph.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                node.Id = Guid.NewGuid().ToString("N");
            }

            node.TypeId = node.TypeId?.Trim() ?? string.Empty;
            node.Properties ??= new();
        }

        foreach (var connection in graph.Connections)
        {
            if (string.IsNullOrWhiteSpace(connection.Id))
            {
                connection.Id = Guid.NewGuid().ToString("N");
            }

            connection.SourceNodeId = connection.SourceNodeId?.Trim() ?? string.Empty;
            connection.SourcePortId = connection.SourcePortId?.Trim() ?? string.Empty;
            connection.TargetNodeId = connection.TargetNodeId?.Trim() ?? string.Empty;
            connection.TargetPortId = connection.TargetPortId?.Trim() ?? string.Empty;
        }
    }

    private static Dictionary<string, NodeInstance> BuildNodeIndex(
        NodeGraph graph,
        List<GraphDiagnostic> diagnostics)
    {
        var nodesById = new Dictionary<string, NodeInstance>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in graph.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                diagnostics.Add(Error("node.id.required", "Node id is required."));
                continue;
            }

            if (!nodesById.TryAdd(node.Id, node))
            {
                diagnostics.Add(Error("node.id.duplicate", $"Duplicate node id: {node.Id}", node.Id));
            }
        }

        return nodesById;
    }

    private Dictionary<string, NodeTypeDefinition> ResolveNodeTypes(
        Dictionary<string, NodeInstance> nodesById,
        List<GraphDiagnostic> diagnostics)
    {
        var nodeTypesByNodeId = new Dictionary<string, NodeTypeDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodesById.Values)
        {
            if (string.IsNullOrWhiteSpace(node.TypeId))
            {
                diagnostics.Add(Error("node.type.required", "Node type is required.", node.Id));
                continue;
            }

            if (!_nodeTypeCatalog.TryGetNodeType(node.TypeId, out var nodeType) || nodeType is null)
            {
                diagnostics.Add(Error("node.type.unknown", $"Unknown node type: {node.TypeId}", node.Id));
                continue;
            }

            nodeTypesByNodeId[node.Id] = nodeType;
        }

        return nodeTypesByNodeId;
    }

    private static void ValidateConnections(
        NodeGraph graph,
        Dictionary<string, NodeInstance> nodesById,
        Dictionary<string, NodeTypeDefinition> nodeTypesByNodeId,
        List<GraphDiagnostic> diagnostics)
    {
        var inputConnections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var connectionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var connection in graph.Connections)
        {
            if (!connectionIds.Add(connection.Id))
            {
                diagnostics.Add(Error(
                    "connection.id.duplicate",
                    $"Duplicate connection id: {connection.Id}",
                    connectionId: connection.Id));
            }

            var sourceNode = GetNode(connection.SourceNodeId, nodesById, connection.Id, "source", diagnostics);
            var targetNode = GetNode(connection.TargetNodeId, nodesById, connection.Id, "target", diagnostics);

            if (sourceNode is null || targetNode is null)
            {
                continue;
            }

            if (string.Equals(sourceNode.Id, targetNode.Id, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(Error(
                    "connection.self",
                    "Connection cannot target the same node it starts from.",
                    sourceNode.Id,
                    connection.Id));
            }

            if (!nodeTypesByNodeId.TryGetValue(sourceNode.Id, out var sourceType)
                || !nodeTypesByNodeId.TryGetValue(targetNode.Id, out var targetType))
            {
                continue;
            }

            var sourcePort = FindPort(sourceType, connection.SourcePortId, NodePortDirection.Output);
            var targetPort = FindPort(targetType, connection.TargetPortId, NodePortDirection.Input);

            if (sourcePort is null)
            {
                diagnostics.Add(Error(
                    "connection.sourcePort.unknown",
                    $"Unknown output port '{connection.SourcePortId}' on node '{sourceNode.Id}'.",
                    sourceNode.Id,
                    connection.Id,
                    connection.SourcePortId));
                continue;
            }

            if (targetPort is null)
            {
                diagnostics.Add(Error(
                    "connection.targetPort.unknown",
                    $"Unknown input port '{connection.TargetPortId}' on node '{targetNode.Id}'.",
                    targetNode.Id,
                    connection.Id,
                    connection.TargetPortId));
                continue;
            }

            if (sourcePort.ValueType != targetPort.ValueType)
            {
                diagnostics.Add(Error(
                    "connection.typeMismatch",
                    $"Cannot connect {sourcePort.ValueType} output to {targetPort.ValueType} input.",
                    targetNode.Id,
                    connection.Id,
                    connection.TargetPortId));
            }

            var inputKey = $"{targetNode.Id}:{targetPort.Id}";
            if (!targetPort.AllowMultipleConnections && !inputConnections.Add(inputKey))
            {
                diagnostics.Add(Error(
                    "connection.input.multiple",
                    $"Input port '{targetPort.Id}' on node '{targetNode.Id}' already has a connection.",
                    targetNode.Id,
                    connection.Id,
                    targetPort.Id));
            }
        }
    }

    private static IReadOnlyList<NodeInstance> BuildEvaluationOrder(
        NodeGraph graph,
        Dictionary<string, NodeInstance> nodesById,
        List<GraphDiagnostic> diagnostics)
    {
        var outgoing = nodesById.Keys.ToDictionary(
            nodeId => nodeId,
            _ => new List<string>(),
            StringComparer.OrdinalIgnoreCase);
        var incomingCounts = nodesById.Keys.ToDictionary(
            nodeId => nodeId,
            _ => 0,
            StringComparer.OrdinalIgnoreCase);

        foreach (var connection in graph.Connections)
        {
            if (!nodesById.ContainsKey(connection.SourceNodeId)
                || !nodesById.ContainsKey(connection.TargetNodeId))
            {
                continue;
            }

            outgoing[connection.SourceNodeId].Add(connection.TargetNodeId);
            incomingCounts[connection.TargetNodeId]++;
        }

        var queue = new Queue<string>(incomingCounts
            .Where(pair => pair.Value == 0)
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Key));
        var orderedNodes = new List<NodeInstance>();

        while (queue.Count > 0)
        {
            var nodeId = queue.Dequeue();
            orderedNodes.Add(nodesById[nodeId]);

            foreach (var targetNodeId in outgoing[nodeId].Order(StringComparer.OrdinalIgnoreCase))
            {
                incomingCounts[targetNodeId]--;

                if (incomingCounts[targetNodeId] == 0)
                {
                    queue.Enqueue(targetNodeId);
                }
            }
        }

        if (orderedNodes.Count != nodesById.Count)
        {
            foreach (var nodeId in incomingCounts
                .Where(pair => pair.Value > 0)
                .Select(pair => pair.Key)
                .Order(StringComparer.OrdinalIgnoreCase))
            {
                diagnostics.Add(Error("graph.cycle", $"Graph contains a cycle involving node '{nodeId}'.", nodeId));
            }
        }

        return orderedNodes;
    }

    private static void ValidateOutputs(
        NodeGraph graph,
        Dictionary<string, NodeTypeDefinition> nodeTypesByNodeId,
        List<GraphDiagnostic> diagnostics)
    {
        if (graph.Nodes.Count == 0)
        {
            diagnostics.Add(Warning("graph.empty", "Graph has no nodes."));
            return;
        }

        var hasOutputNode = graph.Nodes.Any(node =>
            nodeTypesByNodeId.TryGetValue(node.Id, out var nodeType)
            && nodeType.Category.Equals("Outputs", StringComparison.OrdinalIgnoreCase));

        if (!hasOutputNode)
        {
            diagnostics.Add(Warning("graph.output.missing", "Graph has no output node yet."));
        }
    }

    private static NodeInstance? GetNode(
        string nodeId,
        Dictionary<string, NodeInstance> nodesById,
        string connectionId,
        string role,
        List<GraphDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            diagnostics.Add(Error(
                $"connection.{role}.required",
                $"Connection {role} node id is required.",
                connectionId: connectionId));
            return null;
        }

        if (!nodesById.TryGetValue(nodeId, out var node))
        {
            diagnostics.Add(Error(
                $"connection.{role}.unknown",
                $"Connection references unknown {role} node '{nodeId}'.",
                nodeId,
                connectionId));
            return null;
        }

        return node;
    }

    private static NodePortDefinition? FindPort(
        NodeTypeDefinition nodeType,
        string portId,
        NodePortDirection direction)
    {
        var ports = direction == NodePortDirection.Input ? nodeType.Inputs : nodeType.Outputs;
        return ports.FirstOrDefault(port => string.Equals(port.Id, portId, StringComparison.OrdinalIgnoreCase));
    }

    private static GraphDiagnostic Error(
        string code,
        string message,
        string? nodeId = null,
        string? connectionId = null,
        string? portId = null)
    {
        return new GraphDiagnostic(GraphDiagnosticSeverity.Error, code, message, nodeId, connectionId, portId);
    }

    private static GraphDiagnostic Warning(
        string code,
        string message,
        string? nodeId = null,
        string? connectionId = null,
        string? portId = null)
    {
        return new GraphDiagnostic(GraphDiagnosticSeverity.Warning, code, message, nodeId, connectionId, portId);
    }
}
