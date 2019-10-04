using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Script
{
    public class CachingScriptMetadataResolver : MetadataReferenceResolver
    {
        private readonly MetadataReferenceResolver _defaultReferenceResolver;
        private static ConcurrentDictionary<string, ImmutableArray<PortableExecutableReference>> DirectReferenceCache = new ConcurrentDictionary<string, ImmutableArray<PortableExecutableReference>>();
        private static ConcurrentDictionary<string, PortableExecutableReference> MissingReferenceCache = new ConcurrentDictionary<string, PortableExecutableReference>();

        public CachingScriptMetadataResolver(MetadataReferenceResolver defaultReferenceResolver)
        {
            _defaultReferenceResolver = defaultReferenceResolver;
        }

        public override bool Equals(object other)
        {
            return _defaultReferenceResolver.Equals(other);
        }

        public override int GetHashCode()
        {
            return _defaultReferenceResolver.GetHashCode();
        }

        public override bool ResolveMissingAssemblies => _defaultReferenceResolver.ResolveMissingAssemblies;

        public override PortableExecutableReference ResolveMissingAssembly(MetadataReference definition, AssemblyIdentity referenceIdentity)
        {
            return MissingReferenceCache.GetOrAdd(referenceIdentity.Name, _ => _defaultReferenceResolver.ResolveMissingAssembly(definition, referenceIdentity));
        }

        public override ImmutableArray<PortableExecutableReference> ResolveReference(string reference, string baseFilePath, MetadataReferenceProperties properties)
        {
            var key = $"{reference}-{baseFilePath}";
            return DirectReferenceCache.GetOrAdd(key, _ => _defaultReferenceResolver.ResolveReference(reference, baseFilePath, properties));
        }
    }
}
