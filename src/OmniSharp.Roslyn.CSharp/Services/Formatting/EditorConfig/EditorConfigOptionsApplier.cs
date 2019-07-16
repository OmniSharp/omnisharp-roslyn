// adapted from https://github.com/dotnet/format/blob/master/src/Utilities/EditorConfigOptionsApplier.cs
// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.CodingConventions;

namespace OmniSharp.Roslyn.CSharp.Services.Formatting.EditorConfig
{
    internal class EditorConfigOptionsApplier
    {
        private static readonly List<(IOption, OptionStorageLocation, MethodInfo)> _optionsWithStorage;
        private readonly ILogger _logger;

        static EditorConfigOptionsApplier()
        {
            _optionsWithStorage = new List<(IOption, OptionStorageLocation, MethodInfo)>();
            _optionsWithStorage.AddRange(GetPropertyBasedOptionsWithStorageFromTypes(typeof(FormattingOptions), typeof(CSharpFormattingOptions), typeof(SimplificationOptions)));
            _optionsWithStorage.AddRange(GetFieldBasedOptionsWithStorageFromTypes(typeof(CodeStyleOptions), typeof(CSharpFormattingOptions).Assembly.GetType("Microsoft.CodeAnalysis.CSharp.CodeStyle.CSharpCodeStyleOptions")));
        }

        public EditorConfigOptionsApplier(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<EditorConfigOptionsApplier>();
        }

        public OptionSet ApplyConventions(OptionSet optionSet, ICodingConventionsSnapshot codingConventions, string languageName)
        {
            try
            {
                var adjustedConventions = codingConventions.AllRawConventions.ToDictionary(kvp => kvp.Key, kvp => (string)kvp.Value);
                _logger.LogDebug($"All raw discovered .editorconfig options: {string.Join(Environment.NewLine, adjustedConventions.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");

                foreach (var optionWithStorage in _optionsWithStorage)
                {
                    if (TryGetConventionValue(optionWithStorage, adjustedConventions, out var value))
                    {
                        var option = optionWithStorage.Item1;
                        _logger.LogTrace($"Applying .editorconfig option {option.Name}");
                        var optionKey = new OptionKey(option, option.IsPerLanguage ? languageName : null);
                        optionSet = optionSet.WithChangedOption(optionKey, value);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "There was an error when applying .editorconfig options.");
            }

            return optionSet;
        }

        internal static IEnumerable<(IOption, OptionStorageLocation, MethodInfo)> GetPropertyBasedOptionsWithStorageFromTypes(params Type[] types)
            => types
                .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.GetProperty))
                .Where(p => typeof(IOption).IsAssignableFrom(p.PropertyType)).Select(p => (IOption)p.GetValue(null))
                .Select(GetOptionWithStorage).Where(ows => ows.Item2 != null);

        internal static IEnumerable<(IOption, OptionStorageLocation, MethodInfo)> GetFieldBasedOptionsWithStorageFromTypes(params Type[] types)
            => types
                .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                .Where(p => typeof(IOption).IsAssignableFrom(p.FieldType)).Select(p => (IOption)p.GetValue(null))
                .Select(GetOptionWithStorage).Where(ows => ows.Item2 != null);

        internal static (IOption, OptionStorageLocation, MethodInfo) GetOptionWithStorage(IOption option)
        {
            var editorConfigStorage = !option.StorageLocations.IsDefaultOrEmpty
                ? option.StorageLocations.FirstOrDefault(IsEditorConfigStorage)
                : null;

            var tryGetOptionMethod = editorConfigStorage?.GetType().GetMethod("TryGetOption", new[] { typeof(IReadOnlyDictionary<string, string>), typeof(Type), typeof(object).MakeByRefType() });
            return (option, editorConfigStorage, tryGetOptionMethod);
        }

        internal static bool IsEditorConfigStorage(OptionStorageLocation storageLocation)
            => storageLocation.GetType().FullName.StartsWith("Microsoft.CodeAnalysis.Options.EditorConfigStorageLocation") ||
               storageLocation.GetType().FullName.StartsWith("Microsoft.CodeAnalysis.Options.NamingStylePreferenceEditorConfigStorageLocation");

        internal static bool TryGetConventionValue((IOption, OptionStorageLocation, MethodInfo) optionWithStorage, Dictionary<string, string> adjustedConventions, out object value)
        {
            var (option, editorConfigStorage, tryGetOptionMethod) = optionWithStorage;
            value = null;

            var args = new object[] { adjustedConventions, option.Type, value };

            var isOptionPresent = (bool)tryGetOptionMethod.Invoke(editorConfigStorage, args);
            value = args[2];

            return isOptionPresent;
        }
    }
}
