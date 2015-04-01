using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class SignatureHelpItem
    {
        public string Name { get; set; }

        public string Documentation { get; set; }

        public IEnumerable<SignatureHelpParameter> Parameters { get; set; }
    }
}