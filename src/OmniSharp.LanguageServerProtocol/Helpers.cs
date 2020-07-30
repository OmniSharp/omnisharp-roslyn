using System;
using System.Collections.Generic;
using System.IO;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Models;
using OmniSharp.Models.Diagnostics;

namespace OmniSharp.LanguageServerProtocol
{
    public static class Helpers
    {
        public static Diagnostic ToDiagnostic(this DiagnosticLocation location)
        {
            return new Diagnostic()
            {
                // We don't have a code at the moment
                // Code = quickFix.,
                Message = location.Text,
                Range = location.ToRange(),
                Severity = ToDiagnosticSeverity(location.LogLevel),
                Code = location.Id,
                // TODO: We need to forward this type though if we add something like Vb Support
                Source = "csharp",
            };
        }

        public static Range ToRange(this QuickFix location)
        {
            return new Range()
            {
                Start = new Position()
                {
                    Character = location.Column,
                    Line = location.Line
                },
                End = new Position()
                {
                    Character = location.EndColumn,
                    Line = location.EndLine
                },
            };
        }

        public static OmniSharp.Models.V2.Range FromRange(Range range)
        {
            return new OmniSharp.Models.V2.Range
            {
                Start = new OmniSharp.Models.V2.Point
                {
                    Column = Convert.ToInt32(range.Start.Character),
                    Line = Convert.ToInt32(range.Start.Line),
                },
                End = new OmniSharp.Models.V2.Point
                {
                    Column = Convert.ToInt32(range.End.Character),
                    Line = Convert.ToInt32(range.End.Line),
                },
            };
        }

        public static DiagnosticSeverity ToDiagnosticSeverity(string logLevel)
        {
            // We stringify this value and pass to clients
            // should probably use the enum at somepoint
            if (Enum.TryParse<Microsoft.CodeAnalysis.DiagnosticSeverity>(logLevel, out var severity))
            {
                switch (severity)
                {
                    case Microsoft.CodeAnalysis.DiagnosticSeverity.Error:
                        return DiagnosticSeverity.Error;
                    case Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden:
                        return DiagnosticSeverity.Hint;
                    case Microsoft.CodeAnalysis.DiagnosticSeverity.Info:
                        return DiagnosticSeverity.Information;
                    case Microsoft.CodeAnalysis.DiagnosticSeverity.Warning:
                        return DiagnosticSeverity.Warning;
                }
            }

            return DiagnosticSeverity.Information;
        }

        public static DocumentUri ToUri(string fileName) => DocumentUri.File(fileName);
        public static string FromUri(DocumentUri uri) => uri.GetFileSystemPath().Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        public static Range ToRange((int column, int line) location)
        {
            return new Range()
            {
                Start = ToPosition(location),
                End = ToPosition(location)
            };
        }

        public static Position ToPosition((int column, int line) location)
        {
            return new Position(location.line, location.column);
        }

        public static Position ToPosition(OmniSharp.Models.V2.Point point)
        {
            return new Position(point.Line, point.Column);
        }

        public static Range ToRange((int column, int line) start, (int column, int line) end)
        {
            return new Range()
            {
                Start = new Position(start.line, start.column),
                End = new Position(end.line, end.column)
            };
        }

        public static Range ToRange(OmniSharp.Models.V2.Range range)
        {
            return new Range()
            {
                Start = ToPosition(range.Start),
                End = ToPosition(range.End)
            };
        }

        private static readonly IDictionary<string, SymbolKind> Kinds = new Dictionary<string, SymbolKind>
        {
            { OmniSharp.Models.V2.SymbolKinds.Class, SymbolKind.Class },
            { OmniSharp.Models.V2.SymbolKinds.Delegate, SymbolKind.Class },
            { OmniSharp.Models.V2.SymbolKinds.Enum, SymbolKind.Enum },
            { OmniSharp.Models.V2.SymbolKinds.Interface, SymbolKind.Interface },
            { OmniSharp.Models.V2.SymbolKinds.Struct, SymbolKind.Struct },
            { OmniSharp.Models.V2.SymbolKinds.Constant, SymbolKind.Constant },
            { OmniSharp.Models.V2.SymbolKinds.Destructor, SymbolKind.Method },
            { OmniSharp.Models.V2.SymbolKinds.EnumMember, SymbolKind.EnumMember },
            { OmniSharp.Models.V2.SymbolKinds.Event, SymbolKind.Event },
            { OmniSharp.Models.V2.SymbolKinds.Field, SymbolKind.Field },
            { OmniSharp.Models.V2.SymbolKinds.Indexer, SymbolKind.Property },
            { OmniSharp.Models.V2.SymbolKinds.Method, SymbolKind.Method },
            { OmniSharp.Models.V2.SymbolKinds.Operator, SymbolKind.Operator },
            { OmniSharp.Models.V2.SymbolKinds.Property, SymbolKind.Property },
            { OmniSharp.Models.V2.SymbolKinds.Namespace, SymbolKind.Namespace },
            { OmniSharp.Models.V2.SymbolKinds.Unknown, SymbolKind.Class },
        };

        public static SymbolKind ToSymbolKind(string omnisharpKind)
        {
            return Kinds.TryGetValue(omnisharpKind.ToLowerInvariant(), out var symbolKind) ? symbolKind : SymbolKind.Class;
        }
    }
}
