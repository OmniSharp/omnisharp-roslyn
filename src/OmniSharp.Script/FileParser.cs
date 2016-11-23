using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace OmniSharp.Script
{
    public class FileParser
    {
        private readonly string _workingDirectory;
        private readonly FileParserResult _result;

        public FileParser(string workingDirectory)
        {
            _workingDirectory = workingDirectory;
            _result = new FileParserResult();
        }

        public FileParserResult ProcessFile(string path)
        {
            ParseFile(path);
            return _result;
        }

        private void ParseFile(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (_result.LoadedScripts.Contains(fullPath))
            {
                return;
            }

            _result.LoadedScripts.Add(fullPath);

            var scriptCode = File.ReadAllText(fullPath);

            var syntaxTree = CSharpSyntaxTree.ParseText(scriptCode, CSharpParseOptions.Default.
                WithPreprocessorSymbols("load", "r").
                WithKind(SourceCodeKind.Script).
                WithLanguageVersion(LanguageVersion.Default));

            var namespaces = syntaxTree.GetCompilationUnitRoot().Usings.Select(x => x.Name.ToString());
            foreach (var ns in namespaces)
            {
                _result.Namespaces.Add(ns.Trim());
            }

            var refs = syntaxTree.GetCompilationUnitRoot().GetReferenceDirectives().Select(x => x.File.ToString());
            foreach (var reference in refs)
            {
                _result.References.Add(reference.Replace("\"", string.Empty));
            }

            var loads = syntaxTree.GetCompilationUnitRoot().GetLoadDirectives().Select(x => x.File.ToString());
            foreach (var load in loads)
            {
                var filePath = load.Replace("\"", string.Empty);
                var loadFullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(_workingDirectory, filePath);
                if (!string.IsNullOrWhiteSpace(loadFullPath))
                {
                    ParseFile(loadFullPath);
                }
            }
        }
    }
}