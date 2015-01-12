using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class Node
    {

        public IEnumerable<Node> ChildNodes { get; set; }

        public QuickFix Location { get; set; }

        public string Kind { get; set; }
    }
}