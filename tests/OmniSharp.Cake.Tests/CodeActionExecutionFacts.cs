using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Cake.Services.RequestHandlers.Buffer;
using OmniSharp.Cake.Services.RequestHandlers.Refactoring.V2;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Models.V2;
using OmniSharp.Models.V2.CodeActions;
using Roslyn.Test.Utilities;
using TestUtility;
using Xunit;
using Range = OmniSharp.Models.V2.Range;

namespace OmniSharp.Cake.Tests
{
    public class CodeActionExecutionFacts : CodeActionsTestBase
    {
        public CodeActionExecutionFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task Can_extract_method()
        {
            const string code =
@"public class Class1
{
    public void Whatever()
    {
        [|Console.Write(""should be using System;"");|]
    }
}";

            var expected = new LinePositionSpanTextChange
            {
                NewText = "NewMethod();\n    }\n\n    private static void NewMethod()\n    {\n        ",
                StartLine = 4,
                StartColumn = 8,
                EndLine = 4,
                EndColumn = 8
            };

            var response = await RunRefactoringAsync(code, "Extract method");
            var modifiedFile = response.Changes.FirstOrDefault() as ModifiedFileResponse;

            Assert.Single(response.Changes);
            Assert.NotNull(modifiedFile);
            Assert.Single(modifiedFile.Changes);
            Assert.Equal(expected, modifiedFile.Changes.FirstOrDefault());
        }
    }
}
