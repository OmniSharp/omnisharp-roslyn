// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

// This is a modified copy originally taken from https://github.com/dotnet/roslyn/blob/0a2f70279c4d0125a51a5751dadff345268ece58/src/Workspaces/Core/Desktop/Workspace/MSBuild/SolutionFile/SectionBlock.cs

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace OmniSharp.MSBuild.SolutionParsing
{
    /// <summary>
    /// Represents a SectionBlock in a .sln file. Section blocks are of the form:
    /// 
    /// Type(ParenthesizedName) = Value
    ///     Key = Value
    ///     [more keys/values]
    /// EndType
    /// </summary>
    internal sealed partial class SectionBlock
    {
        public string Type { get; }
        public string ParenthesizedName { get; }
        public string Value { get; }
        public ReadOnlyCollection<KeyValuePair<string, string>> KeyValuePairs { get; }

        private SectionBlock(string type, string parenthesizedName, string value, ReadOnlyCollection<KeyValuePair<string, string>> keyValuePairs)
        {
            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentException(string.Format(Constants._0_must_be_a_non_null_and_non_empty_string, "type"));
            }

            if (string.IsNullOrEmpty(parenthesizedName))
            {
                throw new ArgumentException(string.Format(Constants._0_must_be_a_non_null_and_non_empty_string, "parenthesizedName"));
            }

            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(string.Format(Constants._0_must_be_a_non_null_and_non_empty_string, "value"));
            }

            Type = type;
            ParenthesizedName = parenthesizedName;
            Value = value;
            KeyValuePairs = keyValuePairs;
        }

        internal string GetText(int indent)
        {
            var builder = new StringBuilder();

            builder.Append('\t', indent);
            builder.AppendFormat("{0}({1}) = ", Type, ParenthesizedName);
            builder.AppendLine(Value);

            foreach (var pair in KeyValuePairs)
            {
                builder.Append('\t', indent + 1);
                builder.Append(pair.Key);
                builder.Append(" = ");
                builder.AppendLine(pair.Value);
            }

            builder.Append('\t', indent);
            builder.AppendFormat("End{0}", Type);
            builder.AppendLine();

            return builder.ToString();
        }

        internal static SectionBlock Parse(TextReader reader)
        {
            string startLine;
            while ((startLine = reader.ReadLine()) != null)
            {
                startLine = startLine.TrimStart(null);
                if (startLine != string.Empty)
                {
                    break;
                }
            }

            var scanner = new LineScanner(startLine);

            var type = scanner.ReadUpToAndEat("(");
            var parenthesizedName = scanner.ReadUpToAndEat(") = ");
            var sectionValue = scanner.ReadRest();

            var keyValuePairs = new List<KeyValuePair<string, string>>();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.TrimStart(null);

                // ignore empty lines
                if (line == string.Empty)
                {
                    continue;
                }

                if (line == "End" + type)
                {
                    break;
                }

                scanner = new LineScanner(line);
                var key = scanner.ReadUpToAndEat(" = ");
                var value = scanner.ReadRest();

                keyValuePairs.Add(new KeyValuePair<string, string>(key, value));
            }

            return new SectionBlock(type, parenthesizedName, sectionValue, keyValuePairs.AsReadOnly());
        }
    }
}
