namespace LucaLights.Core.NodeEngine;

public interface INodeTypeCatalog
{
    IReadOnlyList<NodeTypeDefinition> GetNodeTypes();

    bool TryGetNodeType(string typeId, out NodeTypeDefinition? nodeType);
}
