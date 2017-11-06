using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace OmniSharp.MSBuild.ProjectFile
{
    internal static class ProjectFileInfoExtensions
    {
        public static CSharpCompilationOptions CreateCompilationOptions(this ProjectFileInfo projectFileInfo)
        {
            var result = new CSharpCompilationOptions(projectFileInfo.OutputKind);

            result = result.WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default);

            if (projectFileInfo.AllowUnsafeCode)
            {
                result = result.WithAllowUnsafe(true);
            }

            var specificDiagnosticOptions = new Dictionary<string, ReportDiagnostic>(projectFileInfo.SuppressedDiagnosticIds.Count)
            {
                // Ensure that specific warnings about assembly references are always suppressed.
                { "CS1701", ReportDiagnostic.Suppress },
                { "CS1702", ReportDiagnostic.Suppress },
                { "CS1705", ReportDiagnostic.Suppress }
            };

            if (projectFileInfo.SuppressedDiagnosticIds.Any())
            {
                foreach (var id in projectFileInfo.SuppressedDiagnosticIds)
                {
                    if (!specificDiagnosticOptions.ContainsKey(id))
                    {
                        specificDiagnosticOptions.Add(id, ReportDiagnostic.Suppress);
                    }
                }
            }

            result = result.WithSpecificDiagnosticOptions(specificDiagnosticOptions);

            if (projectFileInfo.SignAssembly && !string.IsNullOrEmpty(projectFileInfo.AssemblyOriginatorKeyFile))
            {
                var keyFile = Path.Combine(projectFileInfo.Directory, projectFileInfo.AssemblyOriginatorKeyFile);
                result = result.WithStrongNameProvider(new DesktopStrongNameProvider())
                               .WithCryptoKeyFile(keyFile);
            }

            if (!string.IsNullOrWhiteSpace(projectFileInfo.DocumentationFile))
            {
                result = result.WithXmlReferenceResolver(XmlFileResolver.Default);
            }

            return result;
        }

        public static ProjectInfo CreateProjectInfo(this ProjectFileInfo projectFileInfo)
        {
            return ProjectInfo.Create(
                id: projectFileInfo.Id,
                version: VersionStamp.Create(),
                name: projectFileInfo.Name,
                assemblyName: projectFileInfo.AssemblyName,
                language: LanguageNames.CSharp,
                filePath: projectFileInfo.FilePath,
                outputFilePath: projectFileInfo.TargetPath,
                compilationOptions: projectFileInfo.CreateCompilationOptions());
        }
    }
}
