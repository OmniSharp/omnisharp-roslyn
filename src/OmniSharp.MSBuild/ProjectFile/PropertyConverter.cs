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
            if (LanguageVersionFacts.TryParse(propertyValue, out var result))
                return result;

            return LanguageVersion.Default;
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
                case "warnings": return NullableContextOptions.Warnings;
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
