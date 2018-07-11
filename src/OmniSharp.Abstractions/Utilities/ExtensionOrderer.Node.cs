// Adapted from ExtensionOrderer in Roslyn
using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Utilities
{
    static partial class ExtensionOrderer
    {
        internal class Node<TNode>
        {
            public string Name { get; set; }
            public List<string> Before { get; set; }
            public List<string> After { get; set; }
            public TNode Extension { get; set; }
            public HashSet<Node<TNode>> NodesBeforeMeSet { get; set; }

            public static Node<TNode> From<TNodeAttribute>(TNode extension, Func<TNodeAttribute, string> nameExtractor) where TNodeAttribute : Attribute
            {
                string name = string.Empty;
                var attribute = extension.GetType().GetCustomAttribute<TNodeAttribute>();
                if (attribute is TNodeAttribute && !string.IsNullOrEmpty(nameExtractor(attribute)))
                {
                    name = nameExtractor(attribute);
                }
                var orderAttributes = extension.GetType().GetCustomAttributes<ExtensionOrderAttribute>(true);
                return new Node<TNode>(extension, name, orderAttributes);
            }

            private Node(TNode extension, string name, IEnumerable<ExtensionOrderAttribute> orderAttributes)
            {
                Extension = extension;
                Name = name;
                Before = new List<string>();
                After = new List<string>();
                NodesBeforeMeSet = new HashSet<Node<TNode>>();
                foreach (var attribute in orderAttributes)
                {
                    AddAttribute(attribute);
                }
            }

            private void AddAttribute(ExtensionOrderAttribute attribute)
            {
                if (attribute.Before != null)
                    Before.Add(attribute.Before);
                if (attribute.After != null)
                    After.Add(attribute.After);
            }

            internal bool CheckForCycles()
            {
                return CheckForCycles(new HashSet<Node<TNode>>());
            }

            private bool CheckForCycles(HashSet<Node<TNode>> seenNodes)
            {
                if (!seenNodes.Add(this))
                {
                    //Cycle detected
                    return true;
                }

                foreach (var before in this.NodesBeforeMeSet)
                {
                    if (before.CheckForCycles(seenNodes))
                        return true;
                }

                seenNodes.Remove(this);
                return false;
            }
        }
    }
}
