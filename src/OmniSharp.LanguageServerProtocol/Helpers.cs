using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestPlatform.CoreUtilities.Extensions;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.LanguageServerProtocol.Handlers;
using OmniSharp.Models;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Models.Metadata;

namespace OmniSharp.LanguageServerProtocol
{
    public static class Helpers
    {
        public static Diagnostic ToDiagnostic(this DiagnosticLocation location)
        {
            var tags = new List<DiagnosticTag>();
            foreach (var tag in location?.Tags ?? Array.Empty<string>())
            {
                if (tag == "Unnecessary") tags.Add(DiagnosticTag.Unnecessary);
                if (tag == "Deprecated") tags.Add(DiagnosticTag.Deprecated);
            }
            return new Diagnostic()
            {
                // We don't have a code at the moment
                // Code = quickFix.,
                Message = !string.IsNullOrWhiteSpace(location.Text) ? location.Text : location.Id,
                Range = location.ToRange(),
                Severity = ToDiagnosticSeverity(location.LogLevel),
                Code = location.Id,
                // TODO: We need to forward this type though if we add something like Vb Support
                Source = "csharp",
                Tags = tags,
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

        public static DocumentUri ToUri(MetadataSource mds) =>
            new Uri($"omnisharp:/metadata/Project/{mds.ProjectName}/Assembly/{mds.AssemblyName}/Symbol/{mds.TypeName}.cs");

        public static string FromUri(DocumentUri uri) => uri.Scheme switch {
            "file" => uri.GetFileSystemPath().Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar),
            "omnisharp" => FromOmnisharpUriPath(uri.Path),
            _ => uri.Path
        };

        private static readonly Regex OmnisharpMetadataUriPathRegex = new Regex(@"/metadata/Project/(.+)/Assembly/(.+)/Symbol/(.+)\.cs");

        private static string FromOmnisharpUriPath(string path)
        {
            Match m = OmnisharpMetadataUriPathRegex.Match(path);

            if (!m.Success)
            {
                return "";
            }
            else
            {
                string projectName = m.Groups[1].ToString();
                string assemblyName = m.Groups[2].ToString();
                string symbolName = m.Groups[3].ToString();

                string folderize(string path) => string.Join("/", path.Split('.'));

                // please note that path here is formed in accordance to the the schema
                // used in OmniSharp.Roslyn.Extensions.SymbolExtensions.GetFilePathForExternalSymbol()
                return
                    $"$metadata$/Project/{folderize(projectName)}/Assembly/{folderize(assemblyName)}/Symbol/{folderize(symbolName)}.cs"
                    .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }
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
            { OmniSharp.Models.V2.SymbolKinds.Constructor, SymbolKind.Constructor },
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

        public static WorkspaceEdit ToWorkspaceEdit(IEnumerable<FileOperationResponse> responses, WorkspaceEditCapability workspaceEditCapability, DocumentVersions documentVersions)
        {
            workspaceEditCapability ??= new WorkspaceEditCapability();
            workspaceEditCapability.ResourceOperations ??= Array.Empty<ResourceOperationKind>();


            if (workspaceEditCapability.DocumentChanges)
            {
                var documentChanges = new List<WorkspaceEditDocumentChange>();
                foreach (var response in responses)
                {
                    documentChanges.Add(ToWorkspaceEditDocumentChange(response, workspaceEditCapability,
                        documentVersions));

                }

                return new WorkspaceEdit()
                {
                    DocumentChanges = documentChanges
                };
            }
            else
            {
                var changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>>();
                foreach (var response in responses)
                {
                    changes.Add(DocumentUri.FromFileSystemPath(response.FileName), ToTextEdits(response));
                }

                return new WorkspaceEdit()
                {
                    Changes = changes
                };
            }
        }

        public static WorkspaceEditDocumentChange ToWorkspaceEditDocumentChange(FileOperationResponse response, WorkspaceEditCapability workspaceEditCapability, DocumentVersions documentVersions)
        {
            workspaceEditCapability ??= new WorkspaceEditCapability();
            workspaceEditCapability.ResourceOperations ??= Array.Empty<ResourceOperationKind>();

            if (response is ModifiedFileResponse modified)
            {
                return new TextDocumentEdit()
                {
                    Edits = new TextEditContainer(modified.Changes.Select(ToTextEdit)),
                    TextDocument = new OptionalVersionedTextDocumentIdentifier()
                    {
                        Version = documentVersions.GetVersion(DocumentUri.FromFileSystemPath(response.FileName)),
                        Uri = DocumentUri.FromFileSystemPath(response.FileName)
                    },
                };
            }

            if (response is RenamedFileResponse rename && workspaceEditCapability.ResourceOperations.Contains(ResourceOperationKind.Rename))
            {
                return new RenameFile()
                {
                    // Options = new RenameFileOptions()
                    // {
                    //     Overwrite                        = true,
                    //     IgnoreIfExists = false
                    // },
                    NewUri = DocumentUri.FromFileSystemPath(rename.NewFileName).ToString(),
                    OldUri = DocumentUri.FromFileSystemPath(rename.FileName).ToString(),
                };
            }

            return default;
        }

        public static IEnumerable<TextEdit> ToTextEdits(FileOperationResponse response)
        {
            if (!(response is ModifiedFileResponse modified)) yield break;
            foreach (var change in modified.Changes)
            {
                yield return ToTextEdit(change);
            }
        }

        public static TextEdit ToTextEdit(LinePositionSpanTextChange textChange)
        {
            return new TextEdit()
            {
                NewText = textChange.NewText,
                Range = ToRange(
                    (textChange.StartColumn, textChange.StartLine),
                    (textChange.EndColumn, textChange.EndLine)
                )
            };
        }

        public static LinePositionSpanTextChange FromTextEdit(TextEdit textEdit)
            => new LinePositionSpanTextChange
            {
                NewText = textEdit.NewText,
                StartLine = textEdit.Range.Start.Line,
                EndLine = textEdit.Range.End.Line,
                StartColumn = textEdit.Range.Start.Character,
                EndColumn = textEdit.Range.End.Character
            };
    }

    public static class CommandExtensions
    {
        public static T ExtractArguments<T>(this IExecuteCommandParams @params, ISerializer serializer)
            where T : notnull =>
            ExtractArguments<T>(@params.Arguments, serializer);

        public static T ExtractArguments<T>(this Command @params, ISerializer serializer)
            where T : notnull =>
            ExtractArguments<T>(@params.Arguments, serializer);

        public static (T arg1, T2 arg2) ExtractArguments<T, T2>(this IExecuteCommandParams command, ISerializer serializer)
            where T : notnull
            where T2 : notnull =>
            ExtractArguments<T, T2>(command.Arguments, serializer);

        public static (T arg1, T2 arg2) ExtractArguments<T, T2>(this Command command, ISerializer serializer)
            where T : notnull
            where T2 : notnull =>
            ExtractArguments<T, T2>(command.Arguments, serializer);

        public static (T arg1, T2 arg2, T3 arg3) ExtractArguments<T, T2, T3>(this IExecuteCommandParams command, ISerializer serializer)
            where T : notnull
            where T2 : notnull
            where T3 : notnull =>
            ExtractArguments<T, T2, T3>(command.Arguments, serializer);

        public static (T arg1, T2 arg2, T3 arg3) ExtractArguments<T, T2, T3>(this Command command, ISerializer serializer)
            where T : notnull
            where T2 : notnull
            where T3 : notnull =>
            ExtractArguments<T, T2, T3>(command.Arguments, serializer);

        public static (T arg1, T2 arg2, T3 arg3, T4 arg4) ExtractArguments<T, T2, T3, T4>(this IExecuteCommandParams command, ISerializer serializer)
            where T : notnull
            where T2 : notnull
            where T3 : notnull
            where T4 : notnull =>
            ExtractArguments<T, T2, T3, T4>(command.Arguments, serializer);

        public static (T arg1, T2 arg2, T3 arg3, T4 arg4) ExtractArguments<T, T2, T3, T4>(this Command command, ISerializer serializer)
            where T : notnull
            where T2 : notnull
            where T3 : notnull
            where T4 : notnull =>
            ExtractArguments<T, T2, T3, T4>(command.Arguments, serializer);

        public static (T arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5) ExtractArguments<T, T2, T3, T4, T5>(this IExecuteCommandParams command, ISerializer serializer)
            where T : notnull
            where T2 : notnull
            where T3 : notnull
            where T4 : notnull
            where T5 : notnull =>
            ExtractArguments<T, T2, T3, T4, T5>(command.Arguments, serializer);

        public static (T arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5) ExtractArguments<T, T2, T3, T4, T5>(this Command command, ISerializer serializer)
            where T : notnull
            where T2 : notnull
            where T3 : notnull
            where T4 : notnull
            where T5 : notnull =>
            ExtractArguments<T, T2, T3, T4, T5>(command.Arguments, serializer);

        public static (T arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6) ExtractArguments<T, T2, T3, T4, T5, T6>(this IExecuteCommandParams command, ISerializer serializer)
            where T : notnull
            where T2 : notnull
            where T3 : notnull
            where T4 : notnull
            where T5 : notnull
            where T6 : notnull =>
            ExtractArguments<T, T2, T3, T4, T5, T6>(command.Arguments, serializer);

        public static (T arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6) ExtractArguments<T, T2, T3, T4, T5, T6>(this Command command, ISerializer serializer)
            where T : notnull
            where T2 : notnull
            where T3 : notnull
            where T4 : notnull
            where T5 : notnull
            where T6 : notnull =>
            ExtractArguments<T, T2, T3, T4, T5, T6>(command.Arguments, serializer);

        private static T ExtractArguments<T>(JArray args, ISerializer serializer)
            where T : notnull
        {
            args ??= new JArray();
            T arg1 = default;
            if (args.Count > 0) arg1 = args[0].ToObject<T>(serializer.JsonSerializer);
            return arg1!;
        }

        private static (T arg1, T2 arg2) ExtractArguments<T, T2>(JArray args, ISerializer serializer)
            where T : notnull
            where T2 : notnull
        {
            args ??= new JArray();
            T arg1 = default;
            if (args.Count > 0) arg1 = args[0].ToObject<T>(serializer.JsonSerializer);
            T2 arg2 = default;
            if (args.Count > 1) arg2 = args[1].ToObject<T2>(serializer.JsonSerializer);

            return (arg1!, arg2!);
        }

        private static (T arg1, T2 arg2, T3 arg3) ExtractArguments<T, T2, T3>(JArray args, ISerializer serializer)
            where T : notnull
            where T2 : notnull
            where T3 : notnull
        {
            args ??= new JArray();
            T arg1 = default;
            if (args.Count > 0) arg1 = args[0].ToObject<T>(serializer.JsonSerializer);
            T2 arg2 = default;
            if (args.Count > 1) arg2 = args[1].ToObject<T2>(serializer.JsonSerializer);
            T3 arg3 = default;
            if (args.Count > 2) arg3 = args[2].ToObject<T3>(serializer.JsonSerializer);

            return (arg1!, arg2!, arg3!);
        }

        private static (T arg1, T2 arg2, T3 arg3, T4 arg4) ExtractArguments<T, T2, T3, T4>(JArray args, ISerializer serializer)
            where T : notnull
            where T2 : notnull
            where T3 : notnull
            where T4 : notnull
        {
            args ??= new JArray();
            T arg1 = default;
            if (args.Count > 0) arg1 = args[0].ToObject<T>(serializer.JsonSerializer);
            T2 arg2 = default;
            if (args.Count > 1) arg2 = args[1].ToObject<T2>(serializer.JsonSerializer);
            T3 arg3 = default;
            if (args.Count > 2) arg3 = args[2].ToObject<T3>(serializer.JsonSerializer);
            T4 arg4 = default;
            if (args.Count > 3) arg4 = args[3].ToObject<T4>(serializer.JsonSerializer);

            return (arg1!, arg2!, arg3!, arg4!);
        }

        private static (T arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5) ExtractArguments<T, T2, T3, T4, T5>(JArray args, ISerializer serializer)
            where T : notnull
            where T2 : notnull
            where T3 : notnull
            where T4 : notnull
            where T5 : notnull
        {
            args ??= new JArray();
            T arg1 = default;
            if (args.Count > 0) arg1 = args[0].ToObject<T>(serializer.JsonSerializer);
            T2 arg2 = default;
            if (args.Count > 1) arg2 = args[1].ToObject<T2>(serializer.JsonSerializer);
            T3 arg3 = default;
            if (args.Count > 2) arg3 = args[2].ToObject<T3>(serializer.JsonSerializer);
            T4 arg4 = default;
            if (args.Count > 3) arg4 = args[3].ToObject<T4>(serializer.JsonSerializer);
            T5 arg5 = default;
            if (args.Count > 4) arg5 = args[4].ToObject<T5>(serializer.JsonSerializer);

            return (arg1!, arg2!, arg3!, arg4!, arg5!);
        }

        private static (T arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6) ExtractArguments<T, T2, T3, T4, T5, T6>(JArray args, ISerializer serializer)
            where T : notnull
            where T2 : notnull
            where T3 : notnull
            where T4 : notnull
            where T5 : notnull
            where T6 : notnull
        {
            args ??= new JArray();
            T arg1 = default;
            if (args.Count > 0) arg1 = args[0].ToObject<T>(serializer.JsonSerializer);
            T2 arg2 = default;
            if (args.Count > 1) arg2 = args[1].ToObject<T2>(serializer.JsonSerializer);
            T3 arg3 = default;
            if (args.Count > 2) arg3 = args[2].ToObject<T3>(serializer.JsonSerializer);
            T4 arg4 = default;
            if (args.Count > 3) arg4 = args[3].ToObject<T4>(serializer.JsonSerializer);
            T5 arg5 = default;
            if (args.Count > 4) arg5 = args[4].ToObject<T5>(serializer.JsonSerializer);
            T6 arg6 = default;
            if (args.Count > 5) arg6 = args[5].ToObject<T6>(serializer.JsonSerializer);

            return (arg1!, arg2!, arg3!, arg4!, arg5!, arg6!);
        }
    }
}
