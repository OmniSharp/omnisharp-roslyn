using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
            var workspace = new OmnisharpWorkspace();

            var projectInfo = ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                                                 "ProjectName", "AssemblyName", LanguageNames.CSharp, "project.json");

            var document = DocumentInfo.Create(DocumentId.CreateNewId(projectInfo.Id), fileName,
                null, SourceCodeKind.Regular,
                TextLoader.From(TextAndVersion.Create(SourceText.From(source), VersionStamp.Create())),
                fileName);

            workspace.AddProject(projectInfo);
            workspace.AddDocument(document);
            return workspace;
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
    }
}