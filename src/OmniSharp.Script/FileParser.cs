using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace OmniSharp.Script
{
    public class FileParser
    {
        public static FileParserResult ProcessFile(string path)
        {
            var result = new FileParserResult();
            ParseFile(path, result);
            return result;
        }

        private static void ParseFile(string path, FileParserResult result)
        {
            var fullPath = Path.GetFullPath(path);
            if (result.LoadedScripts.Contains(fullPath))
            {
                return;
            }

            result.LoadedScripts.Add(fullPath);

            var scriptCode = File.ReadAllText(fullPath);

            var syntaxTree = CSharpSyntaxTree.ParseText(scriptCode, CSharpParseOptions.Default.
                WithPreprocessorSymbols("load", "r").
                WithKind(SourceCodeKind.Script).
                WithLanguageVersion(LanguageVersion.Default));

            var namespaces = syntaxTree.GetCompilationUnitRoot().Usings.Select(x => x.Name.ToString());
            foreach (var ns in namespaces)
            {
                result.Namespaces.Add(ns.Trim());
            }

            var refs = syntaxTree.GetCompilationUnitRoot().GetReferenceDirectives().Select(x => x.File.ToString());
            foreach (var reference in refs)
            {
                result.References.Add(reference.Replace("\"", string.Empty));
            }

            var loads = syntaxTree.GetCompilationUnitRoot().GetLoadDirectives().Select(x => x.File.ToString());
            foreach (var load in loads)
            {
                var filePath = load.Replace("\"", string.Empty);
                var currentWorkingDirectory = Path.GetDirectoryName(fullPath);

                var loadFullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(currentWorkingDirectory, filePath);
                if (!string.IsNullOrWhiteSpace(loadFullPath))
                {
                    ParseFile(loadFullPath, result);
                }
            }
        }
    }
}