// Adapted from ExtensionOrderer in Roslyn
using System.Collections.Generic;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    internal class Graph<T>
    {
        //Dictionary to map between nodes and the names
        private Dictionary<string, ProviderNode<T>> Nodes { get; }
        private List<ProviderNode<T>> AllNodes { get; }
        private Graph(List<ProviderNode<T>> nodesList)
        {
            Nodes = new Dictionary<string, ProviderNode<T>>();
            AllNodes = nodesList;
        }
        internal static Graph<T> GetGraph(List<ProviderNode<T>> nodesList)
        {
            var graph = new Graph<T>(nodesList);

            foreach (ProviderNode<T> node in graph.AllNodes)
            {
                graph.Nodes[node.ProviderName] = node;
            }

            foreach (ProviderNode<T> node in graph.AllNodes)
            {
                foreach (var before in node.Before)
                {
                    if (graph.Nodes.ContainsKey(before))
                    {
                        var beforeNode = graph.Nodes[before];
                        beforeNode.NodesBeforeMeSet.Add(node);
                    }
                }

                foreach (var after in node.After)
                {
                    if (graph.Nodes.ContainsKey(after))
                    {
                        var afterNode = graph.Nodes[after];
                        node.NodesBeforeMeSet.Add(afterNode);
                    }
                }
            }

            return graph;
        }

        public bool HasCycles()
        {
            foreach (var node in this.AllNodes)
            {
                if (node.CheckForCycles())
                    return true;
            }
            return false;
        }

        public List<T> TopologicalSort()
        {
            List<T> result = new List<T>();
            var seenNodes = new HashSet<ProviderNode<T>>();

            foreach (var node in AllNodes)
            {
                Visit(node, result, seenNodes);
            }

            return result;
        }

        private void Visit(ProviderNode<T> node, List<T> result, HashSet<ProviderNode<T>> seenNodes)
        {
            if (seenNodes.Add(node))
            {
                foreach (var before in node.NodesBeforeMeSet)
                {
                    Visit(before, result, seenNodes);
                }

                result.Add(node.Provider);
            }
        }
    }
}
