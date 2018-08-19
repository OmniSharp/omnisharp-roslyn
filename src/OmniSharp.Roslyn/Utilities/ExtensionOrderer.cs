using System;
using System.Collections.Generic;
using System.Linq;

namespace OmniSharp.Utilities
{
    public static partial class ExtensionOrderer
    {
        /* Returns a sorted order of the nodes if such a sorting exists, else returns the unsorted list */
        public static IEnumerable<TExtension> GetOrderedOrUnorderedList<TExtension, TAttribute>(IEnumerable<TExtension> unsortedList, Func<TAttribute, string> nameExtractor) where TAttribute: Attribute
        {
            var nodesList = unsortedList.Select(elem => Node<TExtension>.From(elem, nameExtractor));
            var graph = Graph<TExtension>.GetGraph(nodesList.ToList());
            return graph.HasCycles() ? unsortedList : graph.TopologicalSort();
        }
    }
}
