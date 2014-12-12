using System.Collections.Generic;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class OutputsMessage
    {
        public FrameworkData FrameworkData { get; set; }

        public IDictionary<string, byte[]> EmbeddedReferences { get; set; }

        public byte[] AssemblyBytes { get; set; }
        public byte[] PdbBytes { get; set; }

        public string AssemblyPath { get; set; }
    }
}