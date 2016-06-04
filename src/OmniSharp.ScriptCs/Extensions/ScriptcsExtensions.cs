using Microsoft.CodeAnalysis;
using ScriptCs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OmniSharp.ScriptCs.Extensions
{
    internal static class ScriptcsExtensions
    {
        private static readonly string BaseAssemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location);

        internal static IEnumerable<MetadataReference> MakeMetadataReferences(this ScriptServices scriptServices, IEnumerable<string> referencesPaths)
        {
            var listOfReferences = new List<MetadataReference>();
            foreach (var importedReference in referencesPaths.Where(x => !x.ToLowerInvariant().Contains("scriptcs.contracts")))
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
