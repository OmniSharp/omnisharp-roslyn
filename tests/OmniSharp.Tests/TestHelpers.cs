using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using OmniSharp.Services;

namespace OmniSharp.Tests
{
    public static class TestHelpers
    {
        public class LineColumn
        {
            public int Line { get; private set; }
            public int Column { get; private set; }

            public LineColumn(int line, int column)
            {
                Line = line;
                Column = column;
            }

            public bool Equals(LineColumn other)
            {
                return this.Line.Equals(other.Line) &&
                       this.Column.Equals(other.Column);
            }
        }

        public class Range
        {
            public LineColumn Start { get; private set; }
            public LineColumn End { get; private set; }

            public Range(LineColumn start, LineColumn end)
            {
                Start = start;
                End = end;
            }

            public bool IsEmpty { get { return Start.Equals(End); } }
        }

        public static LineColumn GetLineAndColumnFromDollar(string text)
        {
            return GetLineAndColumnFromFirstOccurence(text, "$");
        }

        public static Range GetRangeFromDollars(string text)
        {
            var start = GetLineAndColumnFromFirstOccurence(text, "$");
            var end = GetLineAndColumnFromLastOccurence(text, "$");

            return new Range(start, end);
        }

        public static LineColumn GetLineAndColumnFromPercent(string text)
        {
            return GetLineAndColumnFromFirstOccurence(text, "%");
        }

        private static LineColumn GetLineAndColumnFromFirstOccurence(string text, string marker)
        {
            var indexOfChar = text.IndexOf(marker);
            CheckIndex(indexOfChar, marker);
            return GetLineAndColumnFromIndex(text, indexOfChar);
        }

        private static LineColumn GetLineAndColumnFromLastOccurence(string text, string marker)
        {
            var indexOfChar = text.LastIndexOf(marker);
            CheckIndex(indexOfChar, marker);
            return GetLineAndColumnFromIndex(text, indexOfChar);
        }

        private static void CheckIndex(int index, string marker)
        {
            if (index == -1)
                throw new ArgumentException(string.Format("Expected a {0} in test input", marker));
        }

        public static LineColumn GetLineAndColumnFromIndex(string text, int index)
        {
            int lineCount = 1, lastLineEnd = -1;
            for (int i = 0; i < index; i++)
                if (text[i] == '\n')
                {
                    lineCount++;
                    lastLineEnd = i;
                }
            return new LineColumn(lineCount, index - lastLineEnd);
        }

        public static string RemovePercentMarker(string fileContent)
        {
            return fileContent.Replace("%", "");
        }

        public static string RemoveDollarMarker(string fileContent)
        {
            return fileContent.Replace("$", "");
        }

        public static OmnisharpWorkspace CreateCsxWorkspace(string source, string fileName = "dummy.csx")
        {
            var versionStamp = VersionStamp.Create();
            var mscorlib = MetadataReference.CreateFromFile(AssemblyFromType(typeof(object)).Location);
            var systemCore = MetadataReference.CreateFromFile(AssemblyFromType(typeof(Enumerable)).Location);
            var references = new[] { mscorlib, systemCore };
            var workspace = new OmnisharpWorkspace(new HostServicesBuilder(Enumerable.Empty<ICodeActionProvider>()));

            var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp6, DocumentationMode.Parse, SourceCodeKind.Script);

            var projectId = ProjectId.CreateNewId(Guid.NewGuid().ToString());
            var project = ProjectInfo.Create(projectId, VersionStamp.Create(), fileName, $"{fileName}.dll", LanguageNames.CSharp, fileName,
                       compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary), metadataReferences: references, parseOptions: parseOptions,
                       isSubmission: true);

            workspace.AddProject(project);
            var document = DocumentInfo.Create(DocumentId.CreateNewId(project.Id), fileName, null, SourceCodeKind.Script, null, fileName)
                .WithSourceCodeKind(SourceCodeKind.Script)
                .WithTextLoader(TextLoader.From(TextAndVersion.Create(SourceText.From(source), VersionStamp.Create())));

            workspace.AddDocument(document);
            return workspace;
        }

        public static Task<OmnisharpWorkspace> CreateSimpleWorkspace(string source, string fileName = "dummy.cs")
        {
            return CreateSimpleWorkspace(CreatePluginHost(Enumerable.Empty<Assembly>()), new Dictionary<string, string> { { fileName, source } });
        }

        public static Task<OmnisharpWorkspace> CreateSimpleWorkspace(CompositionHost host, string source, string fileName = "dummy.cs")
        {
            return CreateSimpleWorkspace(host, new Dictionary<string, string> { { fileName, source } });
        }

        public static Task<OmnisharpWorkspace> CreateSimpleWorkspace(Dictionary<string, string> sourceFiles)
        {
            return CreateSimpleWorkspace(CreatePluginHost(Enumerable.Empty<Assembly>()), sourceFiles);
        }

        public async static Task<OmnisharpWorkspace> CreateSimpleWorkspace(CompositionHost _host, Dictionary<string, string> sourceFiles)
        {
            var host = _host ?? CreatePluginHost(new[] { typeof(CodeCheckService).GetTypeInfo().Assembly });
            var workspace = host.GetExport<OmnisharpWorkspace>();
            await AddProjectToWorkspace(workspace, "project.json", new[] { "dnx451", "dnxcore50" }, sourceFiles);

            await Task.Delay(50);
            return workspace;
        }

        public static CompositionHost CreatePluginHost(IEnumerable<Assembly> assemblies, Func<ContainerConfiguration, ContainerConfiguration> configure = null)
        {
            return Startup.ConfigureMef(
                new FakeServiceProvider(),
                new FakeOmniSharpOptions().Value,
                assemblies,
                configure);
        }

        public static Task<OmnisharpWorkspace> AddProjectToWorkspace(OmnisharpWorkspace workspace, string filePath, string[] frameworks, Dictionary<string, string> sourceFiles)
        {
            var versionStamp = VersionStamp.Create();
            var mscorlib = MetadataReference.CreateFromFile(AssemblyFromType(typeof(object)).Location);
            var systemCore = MetadataReference.CreateFromFile(AssemblyFromType(typeof(Enumerable)).Location);
            var references = new[] { mscorlib, systemCore };

            foreach (var framework in frameworks)
            {
                var projectInfo = ProjectInfo.Create(ProjectId.CreateNewId(), versionStamp,
                                                     "OmniSharp+" + framework, "AssemblyName",
                                                     LanguageNames.CSharp, filePath, metadataReferences: references);

                workspace.AddProject(projectInfo);
                foreach (var file in sourceFiles)
                {
                    var document = DocumentInfo.Create(DocumentId.CreateNewId(projectInfo.Id), file.Key,
                                                       null, SourceCodeKind.Regular,
                                                       TextLoader.From(TextAndVersion.Create(SourceText.From(file.Value), versionStamp)), file.Key);

                    workspace.AddDocument(document);
                }
            }

            return Task.FromResult(workspace);
        }

        private static Assembly AssemblyFromType(Type type)
        {
            return type.GetTypeInfo().Assembly;
        }

        public static async Task<ISymbol> SymbolFromQuickFix(OmnisharpWorkspace workspace, QuickFix result)
        {
            var document = workspace.GetDocument(result.FileName);
            var sourceText = await document.GetTextAsync();
            var position = sourceText.Lines.GetPosition(new LinePosition(result.Line - 1, result.Column - 1));
            var semanticModel = await document.GetSemanticModelAsync();
            return SymbolFinder.FindSymbolAtPosition(semanticModel, position, workspace);
        }

        public static async Task<IEnumerable<ISymbol>> SymbolsFromQuickFixes(OmnisharpWorkspace workspace, IEnumerable<QuickFix> quickFixes)
        {
            var symbols = new List<ISymbol>();
            foreach (var quickfix in quickFixes)
            {
                symbols.Add(await TestHelpers.SymbolFromQuickFix(workspace, quickfix));
            }
            return symbols;
        }
    }
}
