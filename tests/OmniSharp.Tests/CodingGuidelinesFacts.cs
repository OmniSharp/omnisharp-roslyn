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
        public void UsingsAreOrderedSystemFirstThenAlphabetically()
        {
            var invalidSources = new List<string>();
            foreach (var sourcePath in GetSourcePaths())
            {
                var source = File.ReadAllText(sourcePath);
                var syntaxTree = CSharpSyntaxTree.ParseText(source);
                var usings = ((CompilationUnitSyntax)syntaxTree.GetRoot()).Usings
                                .Where(u => u.Alias == null)
                                .Select(u => u.Name.ToString());

                var sorted = usings.OrderByDescending(u => u.StartsWith("System"))
                                   .ThenBy(u => u);

                if (!usings.SequenceEqual(sorted))
                {
                    invalidSources.Add(sourcePath);
                }
            }

            Assert.False(invalidSources.Any(), $"Following files has unordered using statements: {string.Join(", ", invalidSources)}");
        }

        [Fact]
        public void SourceCodeDoesNotContainTabs()
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
