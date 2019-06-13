// adapted from https://github.com/dotnet/format/blob/master/src/Utilities/EditorConfigOptionsApplier.cs
// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using Microsoft.VisualStudio.CodingConventions;

namespace OmniSharp.Roslyn.CSharp.Services.Formatting.EditorConfig
{
    internal static class EditorConfigOptionExtensions
    {
        public static async Task<OptionSet> WithEditorConfigOptions(this OptionSet optionSet, string path)
        {
            if (!Path.IsPathRooted(path))
            {
                path = Directory.GetCurrentDirectory();
            }

            var codingConventionsManager = CodingConventionsManagerFactory.CreateCodingConventionsManager();
            var optionsApplier = new EditorConfigOptionsApplier();
            var context = await codingConventionsManager.GetConventionContextAsync(path, CancellationToken.None);

            if (context != null && context.CurrentConventions != null)
            {
                return optionsApplier.ApplyConventions(optionSet, context.CurrentConventions, LanguageNames.CSharp);
            }

            return optionSet;
        }
    }
}
