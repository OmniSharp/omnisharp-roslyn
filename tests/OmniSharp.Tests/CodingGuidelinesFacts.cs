using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Tests
{
    public class CodingGuidelinesFacts : AbstractTestFixture
    {
        private readonly ILogger _logger;

        public CodingGuidelinesFacts(ITestOutputHelper output)
            : base(output)
        {
            this._logger = this.LoggerFactory.CreateLogger<CodingGuidelinesFacts>();
        }

        [Fact]
        public void Usings_are_ordered_system_first_then_alphabetically()
        {
            var invalidItems = false;
            foreach (var sourcePath in GetSourcePaths())
            {
                var source = File.ReadAllText(sourcePath);
                var syntaxTree = CSharpSyntaxTree.ParseText(source);
                var usings = ((CompilationUnitSyntax)syntaxTree.GetRoot()).Usings;
                var sorted = usings.OrderBy(u => u, UsingComparer.Instance);

                if (!usings.SequenceEqual(sorted))
                {
                    invalidItems = true;

                    var builder = new StringBuilder();
                    builder.AppendLine($"Usings ordered incorrectly in '{sourcePath}'");
                    builder.AppendLine(string.Join(", ", sorted));
                    this._logger.LogError(builder.ToString());
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
