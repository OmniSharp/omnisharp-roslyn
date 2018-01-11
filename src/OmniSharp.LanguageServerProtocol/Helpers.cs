using System;
using OmniSharp.Extensions.LanguageServer.Models;
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

        public static Uri ToUri(string fileName)
        {
            fileName = fileName.Replace(":", "%3A").Replace("\\", "/");
            if (!fileName.StartsWith("/")) return new Uri($"file:///{fileName}");
            return new Uri($"file://{fileName}");
        }

        public static string FromUri(Uri uri)
        {
            if (uri.Segments.Length > 1)
            {
                // On windows of the Uri contains %3a local path
                // doesn't come out as a proper windows path
                if (uri.Segments[1].IndexOf("%3a", StringComparison.OrdinalIgnoreCase) > -1)
                {
                    return FromUri(new Uri(uri.AbsoluteUri.Replace("%3a", ":").Replace("%3A", ":")));
                }
            }
            return uri.LocalPath;
        }

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

        public static Range ToRange((int column, int line) start, (int column, int line) end)
        {
            return new Range()
            {
                Start = new Position(start.line, start.column),
                End = new Position(end.line, end.column)
            };
        }
    }
}
