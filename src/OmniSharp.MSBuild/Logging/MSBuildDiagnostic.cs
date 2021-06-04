using System;
using OmniSharp.Utilities;

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
        {
            string diagnosticText = null;

            // https://github.com/dotnet/msbuild/blob/v16.8.3/src/Tasks/Resources/Strings.resx#L2155-L2158
            // for MSB3644, we should print a different message on Unix because the default one is Windows-specific
            if (args.Code?.Equals("MSB3644", StringComparison.OrdinalIgnoreCase) == true)
            {
                // https://github.com/dotnet/msbuild/issues/5820
                // older versions of MSBuild incorrectly treat 'net5.0'/'net6.0' moniker as ".NETFramework,Version=v5.0/6.0"
                // this creates a confusing error message which we convert into a more helpful one
                if (args.Message.Contains(".NETFramework,Version=v5.0"))
                {
                    diagnosticText = Platform.Current.OperatingSystem == Utilities.OperatingSystem.Windows
                    ? ErrorMessages.ReferenceAssembliesNotFoundNet50Windows : ErrorMessages.ReferenceAssembliesNotFoundNet50Unix;
                }
                else if (args.Message.Contains(".NETFramework,Version=v6.0"))
                {
                    diagnosticText = Platform.Current.OperatingSystem == Utilities.OperatingSystem.Windows
                    ? ErrorMessages.ReferenceAssembliesNotFoundNet60Windows : ErrorMessages.ReferenceAssembliesNotFoundNet60Unix;
                }
                else
                {
                    diagnosticText = Platform.Current.OperatingSystem != Utilities.OperatingSystem.Windows
                        ? ErrorMessages.ReferenceAssembliesNotFoundUnix : args.Message;
                }
            }

            return new MSBuildDiagnostic(MSBuildDiagnosticSeverity.Error,
                diagnosticText ?? args.Message, args.File, args.ProjectFile, args.Subcategory, args.Code,
                args.LineNumber, args.ColumnNumber, args.EndLineNumber, args.EndColumnNumber);
        }

        public static MSBuildDiagnostic CreateFrom(Microsoft.Build.Framework.BuildWarningEventArgs args)
        {
            return new(MSBuildDiagnosticSeverity.Error,
                           args.Message, args.File, args.ProjectFile, args.Subcategory, args.Code,
                           args.LineNumber, args.ColumnNumber, args.EndLineNumber, args.EndColumnNumber);
        }
    }
}
