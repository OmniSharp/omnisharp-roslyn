using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

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
        }

        public static LineColumn GetLineAndColumnFromDollar(string text)
        {
            var indexOfDollar = text.IndexOf("$");
            
            if (indexOfDollar == -1)
                throw new ArgumentException("Expected a $ sign in test input");

            return GetLineAndColumnFromIndex(text, indexOfDollar);
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

        public static OmnisharpWorkspace CreateSimpleWorkspace(string source, string fileName = "dummy.cs")
        {
            return CreateSimpleWorkspace(new Dictionary<string, string> { { fileName, source } });
        }

        public static OmnisharpWorkspace CreateSimpleWorkspace(Dictionary<string, string> sourceFiles)
        {
            var workspace = new OmnisharpWorkspace();
            var projectId = ProjectId.CreateNewId();
            var versionStamp = VersionStamp.Create();
            var mscorlib = MetadataReference.CreateFromAssembly(AssemblyFromType(typeof(object)));
            var systemCore = MetadataReference.CreateFromAssembly(AssemblyFromType(typeof(Enumerable)));
            var references = new[] { mscorlib, systemCore };
            var projectInfo = ProjectInfo.Create(projectId, versionStamp,
                                                 "ProjectName", "AssemblyName",
                                                 LanguageNames.CSharp, "project.json", metadataReferences: references);

            workspace.AddProject(projectInfo);

            foreach (var file in sourceFiles)
            {
                var document = DocumentInfo.Create(DocumentId.CreateNewId(projectInfo.Id), file.Key,
                    null, SourceCodeKind.Regular,
                    TextLoader.From(TextAndVersion.Create(SourceText.From(file.Value), versionStamp)), file.Key);

                workspace.AddDocument(document);
            }

            return workspace;
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
            foreach(var quickfix in quickFixes)
            {
                symbols.Add(await TestHelpers.SymbolFromQuickFix(workspace, quickfix)); 
            }
            return symbols;
        }

        public static ActionExecutingContext CreateActionExecutingContext(Request req)
        {
            var actionContext = new ActionContext(null, null, null);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilter>(), new Dictionary<string, object> { { "request", req} });
            return actionExecutingContext;
        }
    }
}