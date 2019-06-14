// adapted from https://github.com/dotnet/format/blob/master/src/Utilities/EditorConfigOptionsApplier.cs
// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.VisualStudio.CodingConventions;

namespace OmniSharp.Roslyn.CSharp.Services.Formatting.EditorConfig
{
    internal class EditorConfigOptionsApplier
    {
        private readonly ImmutableArray<(IOption, OptionStorageLocation, MethodInfo)> _formattingOptionsWithStorage;
        private readonly ImmutableArray<(IOption, OptionStorageLocation, MethodInfo)> _codestyleOptionsWithStorage;

        public EditorConfigOptionsApplier()
        {
            var commonOptionsType = typeof(FormattingOptions);
            var csharpOptionsType = typeof(CSharpFormattingOptions);
            var codeStyleOptions = typeof(CodeStyleOptions);
            var csharpCodeStyleOptions = typeof(CSharpFormattingOptions).Assembly.GetType("Microsoft.CodeAnalysis.CSharp.CodeStyle.CSharpCodeStyleOptions");
            _formattingOptionsWithStorage = GetPropertyBasedOptionsWithStorageFromTypes(new[] { commonOptionsType, csharpOptionsType });
            _codestyleOptionsWithStorage = GetFieldBasedOptionsWithStorageFromTypes(new[] { codeStyleOptions, csharpCodeStyleOptions });
        }

        public OptionSet ApplyConventions(OptionSet optionSet, ICodingConventionsSnapshot codingConventions, string languageName)
        {
            foreach (var optionWithStorage in _formattingOptionsWithStorage)
            {
                if (TryGetConventionValue(optionWithStorage, codingConventions, out var value))
                {
                    var option = optionWithStorage.Item1;
                    var optionKey = new OptionKey(option, option.IsPerLanguage ? languageName : null);
                    optionSet = optionSet.WithChangedOption(optionKey, value);
                }
            }

            foreach (var optionWithStorage in _codestyleOptionsWithStorage)
            {
                if (TryGetConventionValue(optionWithStorage, codingConventions, out var value))
                {
                    var option = optionWithStorage.Item1;
                    var optionKey = new OptionKey(option, option.IsPerLanguage ? languageName : null);
                    optionSet = optionSet.WithChangedOption(optionKey, value);
                }
            }

            return optionSet;
        }

        internal ImmutableArray<(IOption, OptionStorageLocation, MethodInfo)> GetPropertyBasedOptionsWithStorageFromTypes(params Type[] types)
        {
            var optionType = typeof(IOption);
            return types
                .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.GetProperty))
                .Where(p => optionType.IsAssignableFrom(p.PropertyType)).Select(p => (IOption)p.GetValue(null))
                .Select(GetOptionWithStorage).Where(ows => ows.Item2 != null).ToImmutableArray();
        }

        internal ImmutableArray<(IOption, OptionStorageLocation, MethodInfo)> GetFieldBasedOptionsWithStorageFromTypes(params Type[] types)
        {
            var optionType = typeof(IOption);
            return types
                .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                .Where(p => optionType.IsAssignableFrom(p.FieldType)).Select(p => (IOption)p.GetValue(null))
                .Select(GetOptionWithStorage).Where(ows => ows.Item2 != null).ToImmutableArray();
        }

        internal (IOption, OptionStorageLocation, MethodInfo) GetOptionWithStorage(IOption option)
        {
            var editorConfigStorage = !option.StorageLocations.IsDefaultOrEmpty
                ? option.StorageLocations.FirstOrDefault(IsEditorConfigStorage)
                : null;
            var tryGetOptionMethod = editorConfigStorage?.GetType().GetMethod("TryGetOption");
            return (option, editorConfigStorage, tryGetOptionMethod);
        }

        internal static bool IsEditorConfigStorage(OptionStorageLocation storageLocation)
        {
            return storageLocation.GetType().FullName.StartsWith("Microsoft.CodeAnalysis.Options.EditorConfigStorageLocation");
        }

        internal static bool TryGetConventionValue((IOption, OptionStorageLocation, MethodInfo) optionWithStorage, ICodingConventionsSnapshot codingConventions, out object value)
        {
            var (option, editorConfigStorage, tryGetOptionMethod) = optionWithStorage;

            value = null;

            // EditorConfigStorageLocation no longer accepts a IReadOnlyDictionary<string, object>. All values should
            // be string so we can convert it into a Dictionary<string, string>
            var adjustedConventions = codingConventions.AllRawConventions.ToDictionary(kvp => kvp.Key, kvp => (string)kvp.Value);
            var args = new object[] { option, adjustedConventions, option.Type, value };

            var isOptionPresent = (bool)tryGetOptionMethod.Invoke(editorConfigStorage, args);
            value = args[3];

            return isOptionPresent;
        }
    }
}
