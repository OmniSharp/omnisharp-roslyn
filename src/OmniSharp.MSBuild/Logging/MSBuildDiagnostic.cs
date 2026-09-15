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

        internal static MSBuildDiagnostic Create(
            MSBuildDiagnosticSeverity severity,
            string message,
            string projectFile)
            => new(severity, message, file: null, projectFile, subcategory: null, code: null,
                lineNumber: 0, columnNumber: 0, endLineNumber: 0, endColumnNumber: 0);
    }
}
