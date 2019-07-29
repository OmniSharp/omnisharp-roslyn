// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Threading.Tasks;
// using OmniSharp.Abstractions.Models.V1.FixAll;
// using OmniSharp.Roslyn.CSharp.Services.Refactoring;
// using TestUtility;
// using Xunit;
// using Xunit.Abstractions;

// namespace OmniSharp.Roslyn.CSharp.Tests
// {
//     public class FixAllFacts
//     {
//         private readonly ITestOutputHelper _testOutput;

//         public FixAllFacts(ITestOutputHelper testOutput)
//         {
//             _testOutput = testOutput;
//         }

//         [Fact]
//         public async Task WhenFileContainsFixableIssuesWithAnalyzersEnabled_ThenFixThemAll()
//         {
//             using (var host = GetHost(true))
//             {
//                 var originalText =
//                 @"
//                     using System.IO;
//                     class C {}
//                 ";

//                 var expectedText =
//                 @"
//                     using System.IO;

//                     internal class C { }
//                 ";

//                 host.AddFilesToWorkspace(new TestFile("a.cs", originalText));

//                 var handler = host.GetRequestHandler<RunFixAllCodeActionService>(OmniSharpEndpoints.RunFixAll);

//                 await handler.Handle(new RunFixAllRequest());

//                 var docAfterUpdate = host.Workspace.CurrentSolution.Projects.SelectMany(x => x.Documents).First(x => x.FilePath.EndsWith("a.cs"));
//                 var text = await docAfterUpdate.GetTextAsync();

//                 AssertUtils.AssertIgnoringIndent(originalText, expectedText);
//             }
//         }

//         [Fact]
//         public Task WhenFixAllIsScopedToDocument_ThenOnlyFixDocumentInsteadOfEverything()
//         {
//             throw new NotImplementedException();
//         }

//         [Fact]
//         public async Task WhenAvailableFixAllActionsAreRequested_ThenReturnThemAtResponse()
//         {
//             using (var host = GetHost(true))
//             {
//                 host.AddFilesToWorkspace(new TestFile("a.cs",
//                 @"
//                     using System.IO;
//                     class C {}
//                 "));

//                 var handler = host.GetRequestHandler<GetFixAllCodeActionService>(OmniSharpEndpoints.GetFixAll);

//                 var result = await handler.Handle(new GetFixAllRequest()
//                 {
//                     Scope = FixAllScope.Solution
//                 });

//                 Assert.Contains(result.Items, x => x.Id == "IDE0055" && x.Message.Contains("Fix formatting"));
//                 Assert.Contains(result.Items, x => x.Id == "IDE0040" && x.Message.Contains("Accessibility modifiers required"));
//             }
//         }

//         private OmniSharpTestHost GetHost(bool roslynAnalyzersEnabled)
//         {
//             return OmniSharpTestHost.Create(testOutput: _testOutput, configurationData: new Dictionary<string, string>() { { "RoslynExtensionsOptions:EnableAnalyzersSupport", roslynAnalyzersEnabled.ToString() } });
//         }
//     }
// }
