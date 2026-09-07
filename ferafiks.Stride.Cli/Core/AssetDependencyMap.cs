using Stride.Core.Assets;
using Stride.Core.Assets.Analysis;
using System.Collections;

namespace ferafiks.Stride.Cli.Core;

// The reason why this isn't a tree, is because assets can have circular dependencies

/// <summary>Contains the relations between assets. Root assets go from</summary>
public sealed class AssetDependencyMap : IEnumerable<AssetDependencyMap.Node>
{
    public List<Node> Nodes { get; } = [];

    public List<AssetId> RootAssets { get; } = [];
    public List<AssetId> IncludedInBuildAssets { get; } = [];

    public IEnumerator<Node> GetEnumerator() => Nodes.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static AssetDependencyMap CreateFromRootAssets(AssetDependencyManager dependencyManager, IEnumerable<AssetId> rootAssets)
    {
        var tree = new AssetDependencyMap();
        var dict = new Dictionary<AssetId, Node>();

        foreach (var item in rootAssets)
        {
            tree.RootAssets.Add(item);
            tree.Nodes.Add(Do(item, null));
        }

        return tree;


        Node Do(AssetId asset, Node? parent)
        {
            if (dict.TryGetValue(asset, out var existingNode))
                return existingNode;
            
            tree.IncludedInBuildAssets.Add(asset);
            var node = new Node(asset, parent);
            dict.Add(asset, node);

            var dependencies = dependencyManager.ComputeDependencies(asset);
            if (dependencies != null)
                foreach (var item in dependencies.LinksOut)
                    node.Nodes.Add(Do(item.Item.Id, node));

            return node;
        }
    }

    public sealed class Node(AssetId asset, Node? parent) : IEnumerable<Node>
    {
        public AssetId Asset { get; } = asset;
        public Node? Parent { get; } = parent;
        public List<Node> Nodes { get; } = [];

        public bool IsParentedTo(AssetId asset)
        {
            var node = this;
            while (node.Parent != null)
            {
                if (node.Asset == asset)
                    return true;
                node = node.Parent;
            }

            return false;
        }

        public IEnumerator<Node> GetEnumerator() => Nodes.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
