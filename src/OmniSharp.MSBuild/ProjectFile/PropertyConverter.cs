using System;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NuGet.Versioning;

namespace OmniSharp.MSBuild.ProjectFile
{
    internal static class PropertyConverter
    {
        public static bool ToBoolean(string propertyValue, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(propertyValue))
            {
                return defaultValue;
            }

            try
            {
                return Convert.ToBoolean(propertyValue);
            }
            catch (FormatException)
            {
                return defaultValue;
            }
        }

        public static LanguageVersion ToLanguageVersion(string propertyValue)
        {
            if (string.IsNullOrWhiteSpace(propertyValue) ||
                propertyValue.Equals("Default", StringComparison.OrdinalIgnoreCase))
            {
                return LanguageVersion.Default;
            }

            // ISO-1, ISO-2, 3, 4, 5, 6, 7 or Default
            switch (propertyValue.ToLower())
            {
                case "latest": return LanguageVersion.Latest;
                case "iso-1": return LanguageVersion.CSharp1;
                case "iso-2": return LanguageVersion.CSharp2;
                case "3": return LanguageVersion.CSharp3;
                case "4": return LanguageVersion.CSharp4;
                case "5": return LanguageVersion.CSharp5;
                case "6": return LanguageVersion.CSharp6;
                case "7": return LanguageVersion.CSharp7;
                case "7.1": return LanguageVersion.CSharp7_1;
                case "7.2": return LanguageVersion.CSharp7_2;
                case "7.3": return LanguageVersion.CSharp7_3;
                case "8.0": return LanguageVersion.CSharp8;
                default: return LanguageVersion.Default;
            }
        }

        public static ImmutableArray<string> SplitList(string propertyValue, char separator)
        {
            if (string.IsNullOrWhiteSpace(propertyValue))
            {
                return ImmutableArray<string>.Empty;
            }

            var builder = ImmutableArray.CreateBuilder<string>();
            var values = propertyValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var value in values)
            {
                builder.Add(value.Trim());
            }

            return builder.ToImmutable();
        }

        public static ImmutableArray<string> ToPreprocessorSymbolNames(string propertyValue)
        {
            if (string.IsNullOrWhiteSpace(propertyValue))
            {
                return ImmutableArray<string>.Empty;
            }

            var values = propertyValue.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);

            return ImmutableArray.CreateRange(values);
        }

        public static ImmutableArray<string> ToSuppressedDiagnosticIds(string propertyValue)
        {
            if (string.IsNullOrWhiteSpace(propertyValue))
            {
                return ImmutableArray<string>.Empty;
            }

            var builder = ImmutableArray.CreateBuilder<string>();

            // Remove quotes
            propertyValue = propertyValue.Trim('"');
            var values = propertyValue.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var id in values)
            {
                if (ushort.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                {
                    builder.Add("CS" + number.ToString("0000"));
                }
                else
                {
                    builder.Add(id);
                }
            }

            return builder.ToImmutable();
        }

        public static Guid ToGuid(string propertyValue)
        {
            if (!Guid.TryParse(propertyValue, out var result))
            {
                return Guid.Empty;
            }

            return result;
        }

        public static OutputKind ToOutputKind(string propertyValue)
        {
            switch (propertyValue)
            {
                case "Library": return OutputKind.DynamicallyLinkedLibrary;
                case "WinExe": return OutputKind.WindowsApplication;
                case "Exe": return OutputKind.ConsoleApplication;
                default: return OutputKind.ConsoleApplication;
            }
        }

        public static NullableContextOptions ToNullableContextOptions(string propertyValue)
        {
            switch (propertyValue?.ToLowerInvariant())
            {
                case "disable": return NullableContextOptions.Disable;
                case "enable": return NullableContextOptions.Enable;
                case "safeonly": return NullableContextOptions.SafeOnly;
                case "warnings": return NullableContextOptions.Warnings;
                case "safeonlywarnings": return NullableContextOptions.SafeOnlyWarnings;
                default: return NullableContextOptions.Disable;
            }
        }

        public static VersionRange ToVersionRange(string propertyValue)
        {
            if (VersionRange.TryParse(propertyValue.Trim(), out var version))
            {
                return version;
            }

            return null;
        }
    }
}
