using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models.CodeCheck;
using OmniSharp.Models.FilesChanged;
using OmniSharp.Models.FindImplementations;
using OmniSharp.Models.FindUsages;
using OmniSharp.Models.FixUsings;
using OmniSharp.Models.SignatureHelp;
using OmniSharp.Models.TypeLookup;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using OmniSharp.Roslyn.CSharp.Services.Files;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using OmniSharp.Roslyn.CSharp.Services.Refactoring;
using OmniSharp.Roslyn.CSharp.Services.Signatures;
using OmniSharp.Roslyn.CSharp.Services.Types;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.MiscellanousFiles.Tests
{
    public class EndpointFacts : AbstractTestFixture
    {
        public EndpointFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task Returns_only_syntactic_diagnotics()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithMiscFile"))
            {

                var testfile = new TestFile("a.cs", "class C { b a = new b(); int n  }");
                using (var host = CreateOmniSharpHost(testProject.Directory))
                {
                    var filePath = AddTestFile(testProject, testfile);
                    await WaitForFileUpdate(filePath, host);
                    var codeCheckService = host.GetRequestHandler<CodeCheckService>(OmniSharpEndpoints.CodeCheck);
                    var actual = await codeCheckService.Handle(new CodeCheckRequest() { FileName = filePath });
                    Assert.Single(actual.QuickFixes);
                    Assert.Equal("; expected", actual.QuickFixes.First().Text);
                }
            }
        }

        [Fact]
        public async Task Returns_Signature_help()
        {
            const string source =
@"class Program
{
    public static void Main(){
        System.Guid.NewGuid($$);
    }
}";
            var testfile = new TestFile("a.cs", source);
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithMiscFile"))
            {
                using (var host = CreateOmniSharpHost(testProject.Directory))
                {
                    var filePath = AddTestFile(testProject, testfile);
                    await WaitForFileUpdate(filePath, host);
                    var service = host.GetRequestHandler<SignatureHelpService>(OmniSharpEndpoints.SignatureHelp);
                    var point = testfile.Content.GetPointFromPosition();
                    var request = new SignatureHelpRequest()
                    {
                        FileName = filePath,
                        Line = point.Line,
                        Column = point.Offset,
                        Buffer = testfile.Content.Code
                    };

                    var actual = await service.Handle(request);
                    Assert.Single(actual.Signatures);
                    Assert.Equal(0, actual.ActiveParameter);
                    Assert.Equal(0, actual.ActiveSignature);
                    Assert.Equal("NewGuid", actual.Signatures.ElementAt(0).Name);
                    Assert.Empty(actual.Signatures.ElementAt(0).Parameters);
                }
            }
        }

        [Fact]
        public async Task Returns_Implementations()
        {
            const string source = @"
                public class MyClass 
                { 
                    public MyClass() { Fo$$o(); }

                    public void Foo() {}
                }";

            var testfile = new TestFile("a.cs", source);
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithMiscFile"))
            {
                using (var host = CreateOmniSharpHost(testProject.Directory))
                {
                    var filePath = AddTestFile(testProject, testfile);
                    await WaitForFileUpdate(filePath, host);
                    var service = host.GetRequestHandler<FindImplementationsService>(OmniSharpEndpoints.FindImplementations);
                    var point = testfile.Content.GetPointFromPosition();
                    var request = new FindImplementationsRequest()
                    {
                        FileName = filePath,
                        Line = point.Line,
                        Column = point.Offset,
                        Buffer = testfile.Content.Code
                    };

                    var actual = await service.Handle(request);
                    Assert.Single(actual.QuickFixes);
                    Assert.Equal("public void Foo() {}", actual.QuickFixes.First().Text.Trim());
                }
            }
        }

        [Fact]
        public async Task Returns_Usages()
        {
            const string source = @"
    public class F$$oo
    {
        public string prop { get; set; }
    }

    public class FooConsumer
    {
        public FooConsumer()
        {
            var temp = new Foo();
            var prop = foo.prop;
        }
    }";

            var testfile = new TestFile("a.cs", source);
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithMiscFile"))
            {
                using (var host = CreateOmniSharpHost(testProject.Directory))
                {
                    var filePath = AddTestFile(testProject, testfile);
                    await WaitForFileUpdate(filePath, host);
                    var service = host.GetRequestHandler<FindUsagesService>(OmniSharpEndpoints.FindUsages);
                    var point = testfile.Content.GetPointFromPosition();
                    var request = new FindUsagesRequest()
                    {
                        FileName = filePath,
                        Line = point.Line,
                        Column = point.Offset,
                        Buffer = testfile.Content.Code
                    };

                    var actual = await service.Handle(request);
                    Assert.Equal(2, actual.QuickFixes.Count());
                }
            }
        }

        [Fact]
        public async Task Returns_Symbols()
        {
            const string source = @"
    namespace Some.Long.Namespace
                {
                    public class Foo
                    {
                        private string _field = 0;
                        private string AutoProperty { get; }
                        private string Property
                        {
                            get { return _field; }
                            set { _field = value; }
                        }
                        private string Method() {}
                        private string Method(string param) {}

                        private class Nested
                        {
                            private string NestedMethod() {}
                        }
                    }
                }";

            var testfile = new TestFile("a.cs", source);
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithMiscFile"))
            {
                using (var host = CreateOmniSharpHost(testProject.Directory))
                {
                    var filePath = AddTestFile(testProject, testfile);
                    await WaitForFileUpdate(filePath, host);
                    var service = host.GetRequestHandler<FindSymbolsService>(OmniSharpEndpoints.FindSymbols);
                    var actual = await service.Handle(null);
                    var symbols = actual.QuickFixes.Select(q => q.Text);

                    var expected = new[]
                    {
                "Foo",
                "_field",
                "AutoProperty",
                "Property",
                "Method()",
                "Method(string param)",
                "Nested",
                "NestedMethod()"
            };

                    Assert.Equal(expected, symbols);
                }
            }
        }

        [Fact]
        public async Task Returns_FixUsings()
        {
            const string code = @"
namespace nsA
{
    public class classX{}
}

namespace OmniSharp
{
    public class class1
    {
        public method1()
        {
            var c1 = new classX();
        }
    }
}";

            const string expectedCode = @"
using nsA;

namespace nsA
{
    public class classX{}
}

namespace OmniSharp
{
    public class class1
    {
        public method1()
        {
            var c1 = new classX();
        }
    }
}";

            var testfile = new TestFile("a.cs", code);
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithMiscFile"))
            {
                using (var host = CreateOmniSharpHost(testProject.Directory))
                {
                    var filePath = AddTestFile(testProject, testfile);
                    await WaitForFileUpdate(filePath, host);
                    var service = host.GetRequestHandler<FixUsingService>(OmniSharpEndpoints.FixUsings);
                    var request = new FixUsingsRequest
                    {
                        FileName = filePath
                    };

                    var actual = await service.Handle(request);
                    Assert.Equal(expectedCode.Replace("\r\n", "\n"), actual.Buffer.Replace("\r\n", "\n"));
                }
            }
        }

        [Fact]
        public async Task Returns_TypeLookup()
        {
            const string code = @"class F$$oo {}";
            var testfile = new TestFile("a.cs", code);
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithMiscFile"))
            {
                using (var host = CreateOmniSharpHost(testProject.Directory))
                {
                    var filePath = AddTestFile(testProject, testfile);
                    await WaitForFileUpdate(filePath, host);
                    var service = host.GetRequestHandler<TypeLookupService>(OmniSharpEndpoints.TypeLookup);
                    var point = testfile.Content.GetPointFromPosition();
                    var request = new TypeLookupRequest
                    {
                        FileName = filePath,
                        Line = point.Line,
                        Column = point.Offset,
                    };

                    var actual = await service.Handle(request);
                    Assert.Equal("Foo", actual.Type);
                }
            }
        }


        private string AddTestFile(ITestProject testProject, TestFile testfile)
        {
            return testProject.AddDisposableFile(testfile.FileName, testfile.Content.Text.ToString());
        }

        private async Task WaitForFileUpdate(string filePath, OmniSharpTestHost host)
        {
            var fileChangedService = host.GetRequestHandler<OnFilesChangedService>(OmniSharpEndpoints.FilesChanged);
            await fileChangedService.Handle(new[]
            {
                    new FilesChangedRequest
                    {
                        FileName = filePath,
                        ChangeType = FileWatching.FileChangeType.Create
                    }
                });

            await Task.Delay(2000);
        }
    }
}
