﻿using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;

namespace OmniSharp.Script
{
    public class CachingScriptMetadataResolver : MetadataReferenceResolver
    {
        private static Dictionary<string, ImmutableArray<PortableExecutableReference>> DirectReferenceCache = new Dictionary<string, ImmutableArray<PortableExecutableReference>>();
        private static Dictionary<string, PortableExecutableReference> MissingReferenceCache = new Dictionary<string, PortableExecutableReference>();
        private static MetadataReferenceResolver _defaultRuntimeResolver = ScriptMetadataResolver.Default;

        public override bool Equals(object other)
        {
            return _defaultRuntimeResolver.Equals(other);
        }

        public override int GetHashCode()
        {
            return _defaultRuntimeResolver.GetHashCode();
        }

        public override bool ResolveMissingAssemblies => _defaultRuntimeResolver.ResolveMissingAssemblies;

        public override PortableExecutableReference ResolveMissingAssembly(MetadataReference definition, AssemblyIdentity referenceIdentity)
        {
            if (MissingReferenceCache.ContainsKey(referenceIdentity.Name))
            {
                return MissingReferenceCache[referenceIdentity.Name];
            }

            var result = _defaultRuntimeResolver.ResolveMissingAssembly(definition, referenceIdentity);
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

            var result = _defaultRuntimeResolver.ResolveReference(reference, baseFilePath, properties);
            if (result.Length > 0)
            {
                DirectReferenceCache[key] = result;
            }

            return result;
        }
    }
}
