using System;
using System.Text.RegularExpressions;

namespace OmniSharp.MSBuild.SolutionParsing
{
    internal sealed class Property
    {
        // An example of a property line looks like this:
        //      AspNetCompiler.VirtualPath = "/webprecompile"
        // Because website projects now include the target framework moniker as
        // one of their properties, <PROPERTYVALUE> may have an '=' in it. 
        private static readonly Lazy<Regex> s_lazyPropertyLine = new Lazy<Regex>(
            () => new Regex
                (
                "^" // Beginning of line
                + "(?<PROPERTYNAME>[^=]*)"
                + "\\s*=\\s*" // Any amount of whitespace plus "=" plus any amount of whitespace
                + "(?<PROPERTYVALUE>.*)"
                + "$", // End-of-line
                RegexOptions.Compiled)
            );

        public string Name { get; }
        public string Value { get; }

        private Property(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public static Property Parse(string propertyLine)
        {
            var match = s_lazyPropertyLine.Value.Match(propertyLine);

            if (!match.Success)
            {
                return null;
            }

            var name = match.Groups["PROPERTYNAME"].Value.Trim();
            var value = match.Groups["PROPERTYVALUE"].Value.Trim();

            return new Property(name, value);
        }
    }
}
