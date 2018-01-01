using System;
using System.Collections.Generic;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    internal class Graph<T>
    {
        private Dictionary<string, ProviderNode<T>> Nodes;
        private Graph()
        {
            Nodes = new Dictionary<string, ProviderNode<T>>();
        }
        internal static Graph<T> GetGraph(List<ProviderNode<T>> nodesList)
        {
            var graph = new Graph<T>();

            foreach (ProviderNode<T> node in nodesList)
            {
                if (!graph.Nodes.ContainsKey(node.ProviderName))
                    graph.Nodes[node.ProviderName] = node;
            }

            foreach (ProviderNode<T> node in nodesList)
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

        public List<T> TopologicalSort()
        {
            List<T> result = new List<T>();
            var seenNodes = new HashSet<ProviderNode<T>>();

            foreach (var node in Nodes.Values)
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
