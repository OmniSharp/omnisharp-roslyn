using System.Text.RegularExpressions;

namespace OmniSharp.Models
{
    public class DiagnosticLocation : QuickFix
    {
        private static readonly Regex _Pattern = new Regex(@"(.*)\((\d+),(\d+)\):(.*):(.*)");

        public static DiagnosticLocation FromFormattedValue(string value)
        {
            var match = _Pattern.Match(value);
            if (!match.Success)
            {
                return null;
            }
            return new DiagnosticLocation()
            {
                FileName = match.Groups[1].Value,
                Line = int.Parse(match.Groups[2].Value),
                Column = int.Parse(match.Groups[3].Value),
                LogLevel = match.Groups[4].Value,
                Text = match.Groups[5].Value
            };
        }

        public string LogLevel { get; set; }
    }
}