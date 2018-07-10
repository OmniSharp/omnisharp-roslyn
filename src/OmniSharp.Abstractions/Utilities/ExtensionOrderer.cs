using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniSharp.Utilities
{
    static class ExtensionOrderer
    {
        /* Returns a sorted order of the nodes if such a sorting exists, else returns the unsorted list */
        public static IEnumerable<TExtension> GetOrderedOrUnorderedList<TExtension, TAttribute>(IEnumerable<TExtension> unsortedList, Func<TAttribute, string> nameExtractor) where TAttribute: Attribute
        {
            var nodesList = unsortedList.Select(elem => Node<TExtension>.From<TAttribute>(elem, nameExtractor));
            var graph = Graph<TExtension>.GetGraph(nodesList);
            if (graph.HasCycles())
            {
                return unsortedList;
            }

            return graph.TopologicalSort();
        }
    }
}
