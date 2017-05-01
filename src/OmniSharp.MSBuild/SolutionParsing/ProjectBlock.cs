// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

// This is a modified copy originally taken from https://github.com/dotnet/roslyn/blob/0a2f70279c4d0125a51a5751dadff345268ece58/src/Workspaces/Core/Desktop/Workspace/MSBuild/SolutionFile/ProjectBlock.cs

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace OmniSharp.MSBuild.SolutionParsing
{
    internal sealed partial class ProjectBlock
    {
        private static readonly Guid s_csProjectGuid = new Guid("FAE04EC0-301F-11D3-BF4B-00C04F79EFBC");
        private static readonly Guid s_cpsProjectGuid = new Guid("13B669BE-BB05-4DDF-9536-439F39A36129"); // Used by the .NET CLI when it manipulates solution files
        private static readonly Guid s_cpsCsProjectGuid = new Guid("9A19103F-16F7-4668-BE54-9A1E7A4F7556");
        private static readonly Guid s_solutionFolderGuid = new Guid("2150E333-8FDC-42A3-9474-1A3956D46DE8");

        public ProjectKind Kind { get; }
        public Guid ProjectTypeGuid { get; }
        public string ProjectName { get; }
        public string ProjectPath { get; }
        public Guid ProjectGuid { get; }
        public ReadOnlyCollection<SectionBlock> ProjectSections { get; }

        private ProjectBlock(Guid projectTypeGuid, string projectName, string projectPath, Guid projectGuid, ReadOnlyCollection<SectionBlock> projectSections)
        {
            if (string.IsNullOrEmpty(projectName))
            {
                throw new ArgumentException(string.Format(Constants._0_must_be_a_non_null_and_non_empty_string, "projectName"));
            }

            if (string.IsNullOrEmpty(projectPath))
            {
                throw new ArgumentException(string.Format(Constants._0_must_be_a_non_null_and_non_empty_string, "projectPath"));
            }

            ProjectTypeGuid = projectTypeGuid;
            ProjectName = projectName;
            ProjectPath = projectPath;
            ProjectGuid = projectGuid;
            ProjectSections = projectSections;

            if (projectTypeGuid == s_csProjectGuid ||
                projectTypeGuid == s_cpsCsProjectGuid ||
                projectTypeGuid == s_cpsProjectGuid)
            {
                Kind = ProjectKind.CSharpProject;
            }
            else if (projectTypeGuid == s_solutionFolderGuid)
            {
                Kind = ProjectKind.SolutionFolder;
            }
            else
            {
                Kind = ProjectKind.Unknown;
            }
        }

        internal string GetText()
        {
            var builder = new StringBuilder();

            builder.AppendFormat("Project(\"{0}\") = \"{1}\", \"{2}\", \"{3}\"", ProjectTypeGuid.ToString("B").ToUpper(), ProjectName, ProjectPath, ProjectGuid.ToString("B").ToUpper());
            builder.AppendLine();

            foreach (var block in ProjectSections)
            {
                builder.Append(block.GetText(indent: 1));
            }

            builder.AppendLine("EndProject");

            return builder.ToString();
        }

        internal static ProjectBlock Parse(TextReader reader)
        {
            var startLine = reader.ReadLine().TrimStart(null);
            var scanner = new LineScanner(startLine);

            if (scanner.ReadUpToAndEat("(\"") != "Project")
            {
                throw new Exception(string.Format(Constants.Expected_0, "Project"));
            }

            var projectTypeGuid = Guid.Parse(scanner.ReadUpToAndEat("\")"));

            // Read chars up to next quote, must contain "=" with optional leading/trailing whitespaces.
            if (scanner.ReadUpToAndEat("\"").Trim() != "=")
            {
                throw new Exception(Constants.Invalid_project_block_expected_after_Project);
            }

            var projectName = scanner.ReadUpToAndEat("\"");

            // Read chars up to next quote, must contain "," with optional leading/trailing whitespaces.
            if (scanner.ReadUpToAndEat("\"").Trim() != ",")
            {
                throw new Exception(Constants.Invalid_project_block_expected_after_project_name);
            }

            var projectPath = scanner.ReadUpToAndEat("\"");

            // Read chars up to next quote, must contain "," with optional leading/trailing whitespaces.
            if (scanner.ReadUpToAndEat("\"").Trim() != ",")
            {
                throw new Exception(Constants.Invalid_project_block_expected_after_project_path);
            }

            var projectGuid = Guid.Parse(scanner.ReadUpToAndEat("\""));

            var projectSections = new List<SectionBlock>();

            while (char.IsWhiteSpace((char)reader.Peek()))
            {
                projectSections.Add(SectionBlock.Parse(reader));
            }

            // Expect to see "EndProject" but be tolerant with missing tags as in Dev12. 
            // Instead, we may see either P' for "Project" or 'G' for "Global", which will be handled next.
            if (reader.Peek() != 'P' && reader.Peek() != 'G')
            {
                if (reader.ReadLine() != "EndProject")
                {
                    throw new Exception(string.Format(Constants.Expected_0, "EndProject"));
                }
            }

            return new ProjectBlock(projectTypeGuid, projectName, projectPath, projectGuid, projectSections.AsReadOnly());
        }
    }
}
