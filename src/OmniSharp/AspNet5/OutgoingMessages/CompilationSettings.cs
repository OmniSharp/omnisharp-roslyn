// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class CompilationSettings
    {
        public LanguageVersion LanguageVersion { get; set; }
        public IEnumerable<string> Defines { get; set; }
        public WritableCSharpCompilationOptions CompilationOptions { get; set; }

        public override bool Equals(object obj)
        {
#pragma warning disable CS0436 // Type conflicts with imported type
            var settings = obj as CompilationSettings;
#pragma warning restore CS0436 // Type conflicts with imported type
            return settings != null &&
                LanguageVersion.Equals(settings.LanguageVersion) &&
                Enumerable.SequenceEqual(Defines, settings.Defines) &&
                CompilationOptions.Equals(settings.CompilationOptions);
        }

        public override int GetHashCode()
        {
            // These objects are currently POCOs and we're overriding equals
            // so that things like Enumerable.SequenceEqual just work.
            return base.GetHashCode();
        }
    }

    public class WritableCSharpCompilationOptions
    {
        public bool AllowUnsafe { get; set; }
        public int OutputKind { get; set; }
        public int Platform { get; set; }
        public int OptimizationLevel { get; set; }
        public int WarningLevel { get; set; }
        public bool ConcurrentBuild { get; set; }
        public int GeneralDiagnosticOption { get; set; }
        public Dictionary<string, int> SpecificDiagnosticOptions { get; set; }
    }
}