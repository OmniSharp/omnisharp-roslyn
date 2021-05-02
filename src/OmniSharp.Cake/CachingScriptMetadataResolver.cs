using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;

namespace OmniSharp.Cake
{
    internal class CachingScriptMetadataResolver : MetadataReferenceResolver
    {
        private static readonly ConcurrentDictionary<string, ImmutableArray<PortableExecutableReference>> DirectReferenceCache = new ConcurrentDictionary<string, ImmutableArray<PortableExecutableReference>>();
        private static readonly ConcurrentDictionary<string, PortableExecutableReference> MissingReferenceCache = new ConcurrentDictionary<string, PortableExecutableReference>();
        private static readonly MetadataReferenceResolver DefaultRuntimeResolver = ScriptMetadataResolver.Default;

        public override bool Equals(object other)
        {
            return DefaultRuntimeResolver.Equals(other);
        }

        public override int GetHashCode()
        {
            return DefaultRuntimeResolver.GetHashCode();
        }

        public override bool ResolveMissingAssemblies => DefaultRuntimeResolver.ResolveMissingAssemblies;

        public override PortableExecutableReference ResolveMissingAssembly(MetadataReference definition, AssemblyIdentity referenceIdentity)
        {
            if (MissingReferenceCache.TryGetValue(referenceIdentity.Name, out var result))
            {
                return result;
            }

            result = DefaultRuntimeResolver.ResolveMissingAssembly(definition, referenceIdentity);
            if (result != null)
            {
                MissingReferenceCache[referenceIdentity.Name] = result;
            }

            return result;
        }

        public override ImmutableArray<PortableExecutableReference> ResolveReference(string reference, string baseFilePath, MetadataReferenceProperties properties)
        {
            var key = $"{reference}-{baseFilePath}";
            if (DirectReferenceCache.TryGetValue(key, out var result))
            {
                return result;
            }

            result = DefaultRuntimeResolver.ResolveReference(reference, baseFilePath, properties);
            if (result.Length > 0)
            {
                DirectReferenceCache[key] = result;
            }

            return result;
        }
    }
}
