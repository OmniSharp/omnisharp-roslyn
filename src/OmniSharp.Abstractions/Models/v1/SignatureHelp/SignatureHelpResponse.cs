using System.Collections.Generic;

namespace OmniSharp.Models.SignatureHelp
{
    public class SignatureHelpResponse
    {
        public IEnumerable<SignatureHelpItem> Signatures { get; set; }

        public int ActiveSignature { get; set; }
        
        public int ActiveParameter { get; set; }
    }
}