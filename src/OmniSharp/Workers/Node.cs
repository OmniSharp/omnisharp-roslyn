using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Models
{
    public class Node
    {
        public IEnumerable<Node> ChildNodes { get; set; }
        public Location Location { get; set; }
        public string Text { get; set; }
        public string Kind { get; set; }
        
    }
}