using Microsoft.CodeAnalysis;

namespace OmniSharp.Roslyn
{
    public class MetadataDocumentResult
    {
        public MetadataDocumentResult(Document document, string documentPath)
        {
            Document = document;
            DocumentPath = documentPath;
        }

        public Document Document { get; }

        public string DocumentPath { get; }
    }
}
