using System.IO;
using System.Linq;

namespace OmniSharp.Script
{
    public class FileParser
    {
        private readonly string _workingDirectory;
        private const string UsingString = "using ";
        private const string LoadDirective = "#load";
        private const string ReferenceDirective = "#r";
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

            var scriptLines = File.ReadAllLines(fullPath).ToList();
            foreach (var line in scriptLines)
            {
                ProcessLine(line);
            }
        }

        public void ProcessLine(string line)
        {
            if (IsNamespaceLine(line))
            {
                _result.Namespaces.Add(line.Trim(' ')
                    .Replace(UsingString, string.Empty)
                    .Replace("\"", string.Empty)
                    .Replace(";", string.Empty));
                return;
            }

            if (IsDirectiveLine(line, LoadDirective))
            {
                var filePath = GetDirectiveArgument(line, LoadDirective);
                var fullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(_workingDirectory, filePath);
                if (!string.IsNullOrWhiteSpace(fullPath))
                {
                    ParseFile(fullPath);
                }
                return;
            }

            if (IsDirectiveLine(line, ReferenceDirective))
            {
                var argument = GetDirectiveArgument(line, ReferenceDirective);
                if (!string.IsNullOrWhiteSpace(argument))
                {
                    _result.References.Add(argument);
                }

            }
        }

        private static bool IsNamespaceLine(string line)
        {
            return line.Trim(' ').StartsWith(UsingString) && !line.Contains("{") && line.Contains(";") && !line.Contains("=");
        }

        private static bool IsDirectiveLine(string line, string directiveName)
        {
            return line.Trim(' ').StartsWith(directiveName);
        }

        private static string GetDirectiveArgument(string line, string directiveName)
        {
            return line.Replace(directiveName, string.Empty)
                .Trim()
                .Replace("\"", string.Empty)
                .Replace(";", string.Empty);
        }
    }
}