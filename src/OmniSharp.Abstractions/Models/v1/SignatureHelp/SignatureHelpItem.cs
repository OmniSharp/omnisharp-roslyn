using System.Collections.Generic;
using System.Linq;

namespace OmniSharp.Models.SignatureHelp
{
    public class SignatureHelpItem
    {
        public string Name { get; set; }

        public string Label { get; set; }

        public string Documentation { get; set; }

        public IEnumerable<SignatureHelpParameter> Parameters { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as SignatureHelpItem;
            if (other == null)
            {
                return false;
            }

            return Name == other.Name
                && Label == other.Label
                && Documentation == other.Documentation
                && Enumerable.SequenceEqual(Parameters, other.Parameters);
        }

        public override int GetHashCode()
        {
            return 17 * Name.GetHashCode()
                + 23 * Label.GetHashCode()
                + 31 * Documentation.GetHashCode()
                + Enumerable.Aggregate(Parameters, 37, (current, element) => current + element.GetHashCode());
        }
    }
}
