// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

// This is a modified copy originally taken from https://github.com/dotnet/roslyn/blob/0a2f70279c4d0125a51a5751dadff345268ece58/src/Workspaces/Core/Desktop/Workspace/MSBuild/SolutionFile/SolutionFile.cs

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace OmniSharp.MSBuild.SolutionParsing
{
    internal sealed partial class SolutionFile
    {
        public ReadOnlyCollection<string> HeaderLines { get; }
        public string VisualStudioVersionLineOpt { get; }
        public string MinimumVisualStudioVersionLineOpt { get; }
        public ReadOnlyCollection<ProjectBlock> ProjectBlocks { get; }
        public ReadOnlyCollection<SectionBlock> GlobalSectionBlocks { get; }

        private SolutionFile(
            ReadOnlyCollection<string> headerLines,
            string visualStudioVersionLineOpt,
            string minimumVisualStudioVersionLineOpt,
            ReadOnlyCollection<ProjectBlock> projectBlocks,
            ReadOnlyCollection<SectionBlock> globalSectionBlocks)
        {
            HeaderLines = headerLines ?? throw new ArgumentNullException(nameof(headerLines));
            VisualStudioVersionLineOpt = visualStudioVersionLineOpt;
            MinimumVisualStudioVersionLineOpt = minimumVisualStudioVersionLineOpt;
            ProjectBlocks = projectBlocks ?? throw new ArgumentNullException(nameof(projectBlocks));
            GlobalSectionBlocks = globalSectionBlocks ?? throw new ArgumentNullException(nameof(globalSectionBlocks));
        }

        public string GetText()
        {
            var builder = new StringBuilder();

            builder.AppendLine();

            foreach (var headerLine in HeaderLines)
            {
                builder.AppendLine(headerLine);
            }

            foreach (var block in ProjectBlocks)
            {
                builder.Append(block.GetText());
            }

            builder.AppendLine("Global");

            foreach (var block in GlobalSectionBlocks)
            {
                builder.Append(block.GetText(indent: 1));
            }

            builder.AppendLine("EndGlobal");

            return builder.ToString();
        }

        public static SolutionFile Parse(string solutionFileName)
        {
            using (var stream = File.OpenRead(solutionFileName))
            using (var reader = new StreamReader(stream))
            {
                return Parse(reader);
            }
        }

        public static SolutionFile Parse(TextReader reader)
        {
            var headerLines = new List<string>();

            var headerLine1 = GetNextNonEmptyLine(reader);
            if (headerLine1 == null || !headerLine1.StartsWith("Microsoft Visual Studio Solution File", StringComparison.Ordinal))
            {
                throw new Exception(string.Format(Constants.Expected_header_colon_0, "Microsoft Visual Studio Solution File"));
            }

            headerLines.Add(headerLine1);

            // skip comment lines and empty lines
            while (reader.Peek() != -1 && "#\r\n".Contains((char)reader.Peek()))
            {
                headerLines.Add(reader.ReadLine());
            }

            string visualStudioVersionLineOpt = null;
            if (reader.Peek() == 'V')
            {
                visualStudioVersionLineOpt = GetNextNonEmptyLine(reader);
                if (!visualStudioVersionLineOpt.StartsWith("VisualStudioVersion", StringComparison.Ordinal))
                {
                    throw new Exception(string.Format(Constants.Expected_header_colon_0, "VisualStudioVersion"));
                }
            }

            string minimumVisualStudioVersionLineOpt = null;
            if (reader.Peek() == 'M')
            {
                minimumVisualStudioVersionLineOpt = GetNextNonEmptyLine(reader);
                if (!minimumVisualStudioVersionLineOpt.StartsWith("MinimumVisualStudioVersion", StringComparison.Ordinal))
                {
                    throw new Exception(string.Format(Constants.Expected_header_colon_0, "MinimumVisualStudioVersion"));
                }
            }

            var projectBlocks = new List<ProjectBlock>();

            // Parse project blocks while we have them
            while (reader.Peek() == 'P')
            {
                projectBlocks.Add(ProjectBlock.Parse(reader));
                while (reader.Peek() != -1 && "#\r\n".Contains((char)reader.Peek()))
                {
                    // Comments and Empty Lines between the Project Blocks are skipped
                    reader.ReadLine();
                }
            }

            // We now have a global block
            var globalSectionBlocks = ParseGlobal(reader);

            // We should now be at the end of the file
            if (reader.Peek() != -1)
            {
                throw new Exception(Constants.Expected_end_of_file);
            }

            return new SolutionFile(headerLines.AsReadOnly(), visualStudioVersionLineOpt, minimumVisualStudioVersionLineOpt, projectBlocks.AsReadOnly(), globalSectionBlocks);
        }

        private static readonly ReadOnlyCollection<SectionBlock> s_EmptySectionBlocks = new ReadOnlyCollection<SectionBlock>(Array.Empty<SectionBlock>());

        [SuppressMessage("", "RS0001")] // TODO: This suppression should be removed once we have rulesets in place for Roslyn.sln
        private static ReadOnlyCollection<SectionBlock> ParseGlobal(TextReader reader)
        {
            if (reader.Peek() == -1)
            {
                return s_EmptySectionBlocks;
            }

            if (GetNextNonEmptyLine(reader) != "Global")
            {
                throw new Exception(string.Format(Constants.Expected_0_line, "Global"));
            }

            var globalSectionBlocks = new List<SectionBlock>();

            // The blocks inside here are indented
            while (reader.Peek() != -1 && char.IsWhiteSpace((char)reader.Peek()))
            {
                globalSectionBlocks.Add(SectionBlock.Parse(reader));
            }

            if (GetNextNonEmptyLine(reader) != "EndGlobal")
            {
                throw new Exception(string.Format(Constants.Expected_0_line, "EndGlobal"));
            }

            // Consume potential empty lines at the end of the global block
            while (reader.Peek() != -1 && "\r\n".Contains((char)reader.Peek()))
            {
                reader.ReadLine();
            }

            return globalSectionBlocks.AsReadOnly();
        }

        private static string GetNextNonEmptyLine(TextReader reader)
        {
            string line = null;

            do
            {
                line = reader.ReadLine();
            }
            while (line != null && line.Trim() == string.Empty);

            return line;
        }
    }
}
