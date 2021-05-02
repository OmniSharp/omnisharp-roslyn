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
            if (MissingReferenceCache.ContainsKey(referenceIdentity.Name))
            {
                return MissingReferenceCache[referenceIdentity.Name];
            }

            var result = _defaultReferenceResolver.ResolveMissingAssembly(definition, referenceIdentity);
            if (result != null)
            {
                MissingReferenceCache[referenceIdentity.Name] = result;
            }

            return result;
        }

        public override ImmutableArray<PortableExecutableReference> ResolveReference(string reference, string baseFilePath, MetadataReferenceProperties properties)
        {
            var key = $"{reference}-{baseFilePath}";
            if (DirectReferenceCache.ContainsKey(key))
            {
                return DirectReferenceCache[key];
            }

            var result = _defaultReferenceResolver.ResolveReference(reference, baseFilePath, properties);
            if (result.Length > 0)
            {
                DirectReferenceCache[key] = result;
            }

            return result;
        }
    }
}
