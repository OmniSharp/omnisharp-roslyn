using Microsoft.CodeAnalysis;
using ScriptCs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OmniSharp.ScriptCs.Extensions
{
    internal static class EnumerableExtensions
    {
        private static readonly string BaseAssemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location);

        internal static IEnumerable<MetadataReference> ToMetadataReferences(this IEnumerable<string> referencesToImport, ScriptServices scriptServices)
        {
            var listOfReferences = new List<MetadataReference>();
            foreach (var importedReference in referencesToImport.Where(x => !x.ToLowerInvariant().Contains("scriptcs.contracts")))
            {
                if (scriptServices.FileSystem.IsPathRooted(importedReference))
                {
                    if (scriptServices.FileSystem.FileExists(importedReference))
                        listOfReferences.Add(MetadataReference.CreateFromFile(importedReference));
                }
                else
                {
                    listOfReferences.Add(MetadataReference.CreateFromFile(Path.Combine(BaseAssemblyPath, importedReference.ToLower().EndsWith(".dll") ? importedReference : importedReference + ".dll")));
                }
            }

            return listOfReferences;
        }
    }
}
