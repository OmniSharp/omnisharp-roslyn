// Adapted from ExtensionOrderer in Roslyn
using System.Collections.Generic;

namespace OmniSharp.Utilities
{
    static partial class ExtensionOrderer
    {
        internal class Graph<T>
        {
            //Dictionary to map between nodes and the names
            private Dictionary<string, Node<T>> Nodes { get; }
            private List<Node<T>> AllNodes { get; }
            private Graph(List<Node<T>> nodesList)
            {
                Nodes = new Dictionary<string, Node<T>>();
                AllNodes = nodesList;
            }
            internal static Graph<T> GetGraph(List<Node<T>> nodesList)
            {
                var graph = new Graph<T>(nodesList);

                foreach (Node<T> node in graph.AllNodes)
                {
                    graph.Nodes[node.Name] = node;
                }

                foreach (Node<T> node in graph.AllNodes)
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
                var seenNodes = new HashSet<Node<T>>();

                foreach (var node in AllNodes)
                {
                    Visit(node, result, seenNodes);
                }

                return result;
            }

            private void Visit(Node<T> node, List<T> result, HashSet<Node<T>> seenNodes)
            {
                if (seenNodes.Add(node))
                {
                    foreach (var before in node.NodesBeforeMeSet)
                    {
                        Visit(before, result, seenNodes);
                    }

                    result.Add(node.Extension);
                }
            }
        }
    }
}
