using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Abstractions.Services
{
    public interface ISyntaxFeaturesDiscover
    {
        bool NeedSemanticModel { get; }
        
        IEnumerable<string> Discover(SyntaxNode node, SemanticModel semanticModel);
    }
}