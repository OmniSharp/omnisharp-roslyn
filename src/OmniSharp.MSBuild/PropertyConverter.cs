using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace OmniSharp.MSBuild
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

        public static LanguageVersion? ToLanguageVersion(string propertyValue)
        {
            if (string.IsNullOrWhiteSpace(propertyValue) ||
                propertyValue.Equals("Default", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // ISO-1, ISO-2, 3, 4, 5, 6 or Default
            switch (propertyValue.ToLower())
            {
                case "iso-1": return LanguageVersion.CSharp1;
                case "iso-2": return LanguageVersion.CSharp2;
                case "3": return LanguageVersion.CSharp3;
                case "4": return LanguageVersion.CSharp4;
                case "5": return LanguageVersion.CSharp5;
                case "6": return LanguageVersion.CSharp6;
                default: return null;
            }
        }

        public static IList<string> ToDefineConstants(string propertyValue)
        {
            if (string.IsNullOrWhiteSpace(propertyValue))
            {
                return new string[0];
            }

            var values = propertyValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            return new SortedSet<string>(values).ToArray();
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
