using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;
using System.Linq;

namespace OmniSharp.Script
{
    public class FileParserContext
    {
        public string WorkingDirectory { get; }

        public FileParserContext(string workingDirectory)
        {
            WorkingDirectory = workingDirectory;
            Namespaces = new HashSet<string>();
            References = new HashSet<string>();
            LoadedScripts = new HashSet<string>();
        }

        public HashSet<string> Namespaces { get; private set; }

        public HashSet<string> References { get; private set; }

        public HashSet<string> LoadedScripts { get; private set; }
    }

    public interface ILineProcessor
    {
        bool ProcessLine(FilePreProcessor parser, FileParserContext context, string line, bool isBeforeCode);
    }

    public class UsingLineProcessor : ILineProcessor
    {
        private const string UsingString = "using ";

        public bool ProcessLine(FilePreProcessor parser, FileParserContext context, string line, bool isBeforeCode)
        {
            if (!IsUsingLine(line))
            {
                return false;
            }

            context.Namespaces.Add(GetNamespace(line));
            return true;
        }

        private static bool IsUsingLine(string line)
        {
            return line.Trim(' ').StartsWith(UsingString) && !line.Contains("{") && line.Contains(";") && !line.Contains("=");
        }

        private static string GetNamespace(string line)
        {
            return line.Trim(' ')
                .Replace(UsingString, string.Empty)
                .Replace("\"", string.Empty)
                .Replace(";", string.Empty);
        }
    }

    public abstract class DirectiveLineProcessor : ILineProcessor
    {
        protected abstract string DirectiveName { get; }

        private string DirectiveString => $"#{DirectiveName}";

        public bool ProcessLine(FilePreProcessor parser, FileParserContext context, string line, bool isBeforeCode)
        {
            if (!Matches(line))
            {
                return false;
            }

            return ProcessLine(parser, context, line);
        }

        protected string GetDirectiveArgument(string line)
        {
            return line.Replace(DirectiveString, string.Empty)
                .Trim()
                .Replace("\"", string.Empty)
                .Replace(";", string.Empty);
        }

        protected abstract bool ProcessLine(FilePreProcessor parser, FileParserContext context, string line);

        public bool Matches(string line)
        {
            var tokens = line.Trim().Split();
            return tokens[0] == DirectiveString;
        }
    }

    public class ReferenceLineProcessor : DirectiveLineProcessor
    {
        protected override string DirectiveName => "r";

        protected override bool ProcessLine(FilePreProcessor parser, FileParserContext context, string line)
        {
            var argument = GetDirectiveArgument(line);
            if (!string.IsNullOrWhiteSpace(argument))
            {
                context.References.Add(argument);
            }

            return true;
        }
    }

    public class LoadLineProcessor : DirectiveLineProcessor
    {
        protected override string DirectiveName => "load";

        protected override bool ProcessLine(FilePreProcessor parser, FileParserContext context, string line)
        {
            var filePath = GetDirectiveArgument(line);
            var fullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(context.WorkingDirectory, filePath);
            if (!string.IsNullOrWhiteSpace(fullPath))
            {
                parser.ParseFile(fullPath, context);
            }

            return true;
        }
    }

    public class FilePreProcessor
    {
        private readonly string _workingDirectory;
        private readonly IEnumerable<ILineProcessor> _lineProcessors;

        public FilePreProcessor(string workingDirectory, IEnumerable<ILineProcessor> lineProcessors)
        {
            _workingDirectory = workingDirectory;
            _lineProcessors = lineProcessors;
        }

        public virtual FileParserContext ProcessFile(string path)
        {
            var context = new FileParserContext(_workingDirectory);
            ParseFile(path, context);
            return context;
        }

        public virtual void ParseFile(string path, FileParserContext context)
        {
            var fullPath = Path.GetFullPath(path);
            if (context.LoadedScripts.Contains(fullPath))
            {
                return;
            }

            // Add script to loaded collection before parsing to avoid loop.
            context.LoadedScripts.Add(fullPath);

            var scriptLines = File.ReadAllLines(fullPath).ToList();
            ParseScript(scriptLines, context);
        }

        public virtual void ParseScript(List<string> scriptLines, FileParserContext context)
        {
            var codeIndex = scriptLines.FindIndex(IsNonDirectiveLine);

            for (var index = 0; index < scriptLines.Count; index++)
            {
                var line = scriptLines[index];
                var isBeforeCode = index < codeIndex || codeIndex < 0;

                var wasProcessed = _lineProcessors.Any(x => x.ProcessLine(this, context, line, isBeforeCode));
            }
        }

        private bool IsNonDirectiveLine(string line)
        {
            var directiveLineProcessors = _lineProcessors.OfType<DirectiveLineProcessor>();
            return line != null && line.Trim() != string.Empty && !directiveLineProcessors.Any(lp => lp.Matches(line.Trim()));
        }
    }

    [Export, Shared]
    public class ScriptContext
    {
        public HashSet<string> CsxFilesBeingProcessed { get; } = new HashSet<string>();

        // All of the followings are keyed with the file path
        // Each .csx file is wrapped into a project
        public Dictionary<string, ProjectInfo> CsxFileProjects { get; } = new Dictionary<string, ProjectInfo>();
        public Dictionary<string, List<PortableExecutableReference>> CsxReferences { get; } = new Dictionary<string, List<PortableExecutableReference>>();
        public Dictionary<string, List<ProjectInfo>> CsxLoadReferences { get; } = new Dictionary<string, List<ProjectInfo>>();
        public Dictionary<string, List<string>> CsxUsings { get; } = new Dictionary<string, List<string>>();

        public HashSet<MetadataReference> CommonReferences { get; } = new HashSet<MetadataReference>
        {
            MetadataReference.CreateFromFile(Assembly.Load(new AssemblyName("mscorlib")).Location)
        };
        public HashSet<string> CommonUsings { get; } = new HashSet<string> { "System" };

        public string RootPath { get; set; }
    }
}
