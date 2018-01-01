using System;
using System.Collections.Generic;

namespace OmniSharp.Roslyn.CSharp.Services.Refactoring.V2
{
    internal class Graph
    {
        private Dictionary<string, ProviderNode> Nodes;
        private Graph()
        {
            Nodes = new Dictionary<string, ProviderNode>();
        }
        internal static Graph GetGraph(List<ProviderNode> nodesList)
        {
            var graph = new Graph();

            foreach (ProviderNode node in nodesList)
            {
                if (!graph.Nodes.ContainsKey(node.ProviderName))
                    graph.Nodes[node.ProviderName] = node;
            }

            foreach (ProviderNode node in nodesList)
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

        public List<CodeAction> TopologicalSort()
        {
            List<CodeAction> result = new List<CodeAction>();
            var seenNodes = new HashSet<CodeActionNode>();

            foreach(var nodes in Nodes.Values)
            {
                foreach(var node in nodes)
                    Visit(node, result, seenNodes);
            }

            return result;
        }

        private void Visit(CodeActionNode node, List<CodeAction> result, HashSet<CodeActionNode> seenNodes)
        {
            if(seenNodes.Add(node))
            {
                foreach (var before in node.NodesBeforeMeSet)
                {
                    Visit(before, result, seenNodes);
                }
                result.Add(node.CodeAction);
            }
        }

    }
}
