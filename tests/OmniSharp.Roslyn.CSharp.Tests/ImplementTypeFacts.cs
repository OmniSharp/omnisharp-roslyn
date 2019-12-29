using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Models.V2.CodeActions;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class ImplementTypeFacts : AbstractCodeActionsTestFixture
    {
        public ImplementTypeFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [InlineData("PreferAutoProperties", "public string Name { get; set; }")]
        [InlineData("PreferThrowingProperties", "public string Name { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }")]
        [InlineData(null, "public string Name { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }")]
        public async Task ImplementInterface(string implementTypePropertyGenerationBehavior, string expectedChange)
        {
            const string code = @"
interface IFoo
{
    string Name { get; set; }
}

class Foo : I$$Foo
{
}";

            var testFile = new TestFile("test.cs", code);
            var hostProperties = implementTypePropertyGenerationBehavior != null ? new Dictionary<string, string>
            {
                ["ImplementTypeOptions:PropertyGenerationBehavior"] = implementTypePropertyGenerationBehavior
            } : null;
            using (var host = CreateOmniSharpHost(new[] { testFile }, hostProperties))
            {
                var requestHandler = host.GetRequestHandler<RunCodeActionService>(OmniSharpEndpoints.V2.RunCodeAction);
                var point = testFile.Content.GetPointFromPosition();

                var request = new RunCodeActionRequest
                {
                    Line = point.Line,
                    Column = point.Offset,
                    FileName = testFile.FileName,
                    Buffer = testFile.Content.Code,
                    Identifier = "False;False;AssemblyName;global::IFoo;Microsoft.CodeAnalysis.ImplementInterface.AbstractImplementInterfaceService+ImplementInterfaceCodeAction",
                    WantsTextChanges = true,
                    WantsAllCodeActionOperations = true
                };

                var response = await requestHandler.Handle(request);
                var changes = response.Changes.ToArray();

                Assert.Single(changes);
                Assert.NotNull(changes[0].FileName);
                AssertIgnoringIndent(expectedChange, ((ModifiedFileResponse)changes[0]).Changes.First().NewText);
            }
        }

    }
}
