using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Script
{
    public class CachingScriptMetadataResolver : MetadataReferenceResolver
    {
        private readonly MetadataReferenceResolver defaultReferenceResolver;
        private static Dictionary<string, ImmutableArray<PortableExecutableReference>> DirectReferenceCache = new Dictionary<string, ImmutableArray<PortableExecutableReference>>();
        private static Dictionary<string, PortableExecutableReference> MissingReferenceCache = new Dictionary<string, PortableExecutableReference>();


        public CachingScriptMetadataResolver(MetadataReferenceResolver defaultReferenceResolver)
        {
            this.defaultReferenceResolver = defaultReferenceResolver;
        }

        public override bool Equals(object other)
        {
            return defaultReferenceResolver.Equals(other);
        }

        public override int GetHashCode()
        {
            return defaultReferenceResolver.GetHashCode();
        }

        public override bool ResolveMissingAssemblies => defaultReferenceResolver.ResolveMissingAssemblies;

        public override PortableExecutableReference ResolveMissingAssembly(MetadataReference definition, AssemblyIdentity referenceIdentity)
        {
            if (MissingReferenceCache.ContainsKey(referenceIdentity.Name))
            {
                return MissingReferenceCache[referenceIdentity.Name];
            }

            var result = defaultReferenceResolver.ResolveMissingAssembly(definition, referenceIdentity);
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

            var result = defaultReferenceResolver.ResolveReference(reference, baseFilePath, properties);
            if (result.Length > 0)
            {
                DirectReferenceCache[key] = result;
            }

            return result;
        }
    }
}
