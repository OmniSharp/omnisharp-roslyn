using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace OmniSharp.MSBuild.ProjectFile
{
    internal static class PropertyConverter
    {
        public static bool? ToBoolean(string propertyValue)
        {
            if (string.IsNullOrWhiteSpace(propertyValue))
            {
                return null;
            }

            try
            {
                return Convert.ToBoolean(propertyValue);
            }
            catch (FormatException)
            {
                return null;
            }
        }

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
                case "iso-1": return LanguageVersion.CSharp1;
                case "iso-2": return LanguageVersion.CSharp2;
                case "3": return LanguageVersion.CSharp3;
                case "4": return LanguageVersion.CSharp4;
                case "5": return LanguageVersion.CSharp5;
                case "6": return LanguageVersion.CSharp6;
                case "7": return LanguageVersion.CSharp7;
                default: return LanguageVersion.Default;
            }
        }

        public static IList<string> ToDefineConstants(string propertyValue)
        {
            if (string.IsNullOrWhiteSpace(propertyValue))
            {
                return new string[0];
            }

            var values = propertyValue.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);

            return new SortedSet<string>(values).ToArray();
        }

        public static IList<string> ToSuppressDiagnostics(string propertyValue)
        {
            if (string.IsNullOrWhiteSpace(propertyValue))
            {
                return new string[0];
            }

            // Remove quotes
            propertyValue = propertyValue.Trim('"');
            var values = propertyValue.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new SortedSet<string>();

            foreach (var id in values)
            {
                ushort number;
                if (ushort.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                {
                    result.Add("CS" + number.ToString("0000"));
                }
            }

            return result.ToArray();
        }

        public static Guid ToGuid(string propertyValue)
        {
            Guid result;
            if (!Guid.TryParse(propertyValue, out result))
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
    }
}
