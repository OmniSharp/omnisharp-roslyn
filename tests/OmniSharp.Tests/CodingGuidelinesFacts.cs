using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace OmniSharp.Tests
{
    public class CodingGuidelinesFacts
    {
        [Fact]
        public void Usings_are_ordered_system_first_then_alphabetically()
        {
            var invalidItems = false;
            foreach (var sourcePath in GetSourcePaths())
            {
                var source = File.ReadAllText(sourcePath);
                var syntaxTree = CSharpSyntaxTree.ParseText(source);
                var usings = ((CompilationUnitSyntax)syntaxTree.GetRoot()).Usings
                    .Select(u => u.Name.ToString());

                var sorted = usings.OrderByDescending(u => u.StartsWith("System"))
                                   .ThenBy(u => u);

                if (!usings.SequenceEqual(sorted))
                {
                    invalidItems = true;
                    Console.WriteLine("Usings ordered incorrectly in '" + sourcePath + "'");
                    Console.WriteLine(string.Join(", ", sorted));
                }
            }

            Assert.False(invalidItems);
        }

        [Fact]
        public void Source_code_does_not_contain_tabs()
        {
            foreach (var sourcePath in GetSourcePaths())
            {
                var source = File.ReadAllText(sourcePath);
                var syntaxTree = CSharpSyntaxTree.ParseText(source);

                var hasTabs =
                    syntaxTree.GetRoot()
                    .DescendantTrivia(descendIntoTrivia: true)
                    .Any(node => node.IsKind(SyntaxKind.WhitespaceTrivia)
                         && node.ToString().IndexOf('\t') >= 0);

                Assert.False(hasTabs, sourcePath + " should be formatted with spaces");
            }
        }

        private IEnumerable<string> GetSourcePaths()
        {
            var path = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));
            return Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);
        }
    }
}
