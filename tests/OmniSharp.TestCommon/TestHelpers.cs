using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp.TestCommon
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
            var lineCount = 1;
            var lastLineEnd = -1;

            for (var i = 0; i < index; i++)
            {
                if (text[i] == '\n')
                {
                    lineCount++;
                    lastLineEnd = i;
                }
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
