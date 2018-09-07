namespace OmniSharp.MSBuild.Logging
{
    public class MSBuildDiagnostic
    {
        public MSBuildDiagnosticSeverity Severity { get; }
        public string Message { get; }
        public string File { get; }
        public string ProjectFile { get; }
        public string Subcategory { get; }
        public string Code { get; }
        public int LineNumber { get; }
        public int ColumnNumber { get; }
        public int EndLineNumber { get; }
        public int EndColumnNumber { get; }

        private MSBuildDiagnostic(
            MSBuildDiagnosticSeverity severity,
            string message, string file, string projectFile, string subcategory, string code,
            int lineNumber, int columnNumber, int endLineNumber, int endColumnNumber)
        {
            Severity = severity;
            Message = message;
            File = file;
            ProjectFile = projectFile;
            Subcategory = subcategory;
            Code = code;
            LineNumber = lineNumber;
            ColumnNumber = columnNumber;
            EndLineNumber = endLineNumber;
            EndColumnNumber = endColumnNumber;
        }

        public static MSBuildDiagnostic CreateFrom(Microsoft.Build.Framework.BuildErrorEventArgs args)
            => new MSBuildDiagnostic(MSBuildDiagnosticSeverity.Error,
                args.Message, args.File, args.ProjectFile, args.Subcategory, args.Code,
                args.LineNumber, args.ColumnNumber, args.EndLineNumber, args.EndColumnNumber);

        public static MSBuildDiagnostic CreateFrom(Microsoft.Build.Framework.BuildWarningEventArgs args)
            => new MSBuildDiagnostic(MSBuildDiagnosticSeverity.Error,
                args.Message, args.File, args.ProjectFile, args.Subcategory, args.Code,
                args.LineNumber, args.ColumnNumber, args.EndLineNumber, args.EndColumnNumber);
    }
}
