using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace OmniSharp.Script
{
    public class FileParser
    {
        public static FileParserResult ProcessFile(string path, CSharpParseOptions parseOptions)
        {
            var result = new FileParserResult();
            ParseFile(path, result, parseOptions);
            return result;
        }

        private static void ParseFile(string path, FileParserResult result, CSharpParseOptions parseOptions)
        {
            var fullPath = Path.GetFullPath(path);
            var currentWorkingDirectory = Path.GetDirectoryName(fullPath);

            if (result.LoadedScripts.Contains(fullPath))
            {
                return;
            }

            result.LoadedScripts.Add(fullPath);

            var scriptCode = File.ReadAllText(fullPath);

            var syntaxTree = CSharpSyntaxTree.ParseText(scriptCode, parseOptions);

            var namespaces = syntaxTree.GetCompilationUnitRoot().Usings.Select(x => x.Name.ToString());
            foreach (var ns in namespaces)
            {
                result.Namespaces.Add(ns.Trim());
            }

            var refs = syntaxTree.GetCompilationUnitRoot().GetReferenceDirectives().Select(x => x.File.ToString());
            foreach (var reference in refs)
            {
                var escapedReference = reference.Replace("\"", string.Empty);

                // if #r is an absolute path, use it direcly
                // otherwise, make sure it's treated relatively to the current working directory
                var referenceFullPath = Path.IsPathRooted(escapedReference) ? escapedReference : Path.Combine(currentWorkingDirectory, escapedReference);
                result.References.Add(referenceFullPath);
            }

            var loads = syntaxTree.GetCompilationUnitRoot().GetLoadDirectives().Select(x => x.File.ToString());
            foreach (var load in loads)
            {
                var filePath = load.Replace("\"", string.Empty);

                // if #load is an absolute path, use it direcly
                // otherwise, make sure it's treated relatively to the current working directory
                var loadFullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(currentWorkingDirectory, filePath);
                if (!string.IsNullOrWhiteSpace(loadFullPath))
                {
                    ParseFile(loadFullPath, result, parseOptions);
                }
            }
        }
    }
}