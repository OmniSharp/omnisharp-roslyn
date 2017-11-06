using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Roslyn.Utilities
{
    public class MetadataReferenceEqualityComparer : IEqualityComparer<MetadataReference>
    {
        public static MetadataReferenceEqualityComparer Instance { get; } = new MetadataReferenceEqualityComparer();

        public bool Equals(MetadataReference x, MetadataReference y)
            => x is PortableExecutableReference pe1 && y is PortableExecutableReference pe2
                ? StringComparer.OrdinalIgnoreCase.Equals(pe1.FilePath, pe2.FilePath)
                : EqualityComparer<MetadataReference>.Default.Equals(x, y);

        public int GetHashCode(MetadataReference obj)
            => obj is PortableExecutableReference pe
                ? StringComparer.OrdinalIgnoreCase.GetHashCode(pe.FilePath)
                : EqualityComparer<MetadataReference>.Default.GetHashCode(obj);
    }
}
