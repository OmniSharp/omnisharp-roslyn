using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using OmniSharp.Models;

namespace OmniSharp.Abstractions.Services
{
    public interface ISyntaxFeaturesDiscover
    {
        bool NeedSemanticModel { get; }

        IEnumerable<SyntaxFeature> Discover(SyntaxNode node, SemanticModel semanticModel);
    }
}