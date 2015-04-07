namespace OmniSharp.Models
{
    public class SignatureHelpParameter
    {
        public string Name { get; set; }

        public string Label { get; set; }

        public string Documentation { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as SignatureHelpParameter;
            if (other == null)
            {
                return false;
            }

            return Name == other.Name
                && Label == other.Label
                && Documentation == other.Documentation;
        }

        public override int GetHashCode()
        {
            return 17 * Name.GetHashCode()
                + 23 * Label.GetHashCode()
                + 31 * Documentation.GetHashCode();
        }
    }
}
