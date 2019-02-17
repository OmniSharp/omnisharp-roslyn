using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Models.CodeCheck;
using OmniSharp.Models.FilesChanged;
using OmniSharp.Models.FindImplementations;
using OmniSharp.Models.FindSymbols;
using OmniSharp.Models.FindUsages;
using OmniSharp.Models.FixUsings;
using OmniSharp.Models.SignatureHelp;
using OmniSharp.Models.TypeLookup;
using OmniSharp.Roslyn.CSharp.Services.Files;
using OmniSharp.Roslyn.CSharp.Services.Types;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Tests
{
    public class BufferManagerMiscFilesFacts : AbstractTestFixture
    {
        public BufferManagerMiscFilesFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task Adds_Misc_Document_Which_Supports_Only_syntactic_diagnostics()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("EmptyProject"))
            {
                var testfile = new TestFile("a.cs", "class C { b a = new b(); int n  }");
                using (var host = CreateOmniSharpHost(testProject.Directory))
                {
                    var filePath = await AddTestFile(host, testProject, testfile);
                    var request = new CodeCheckRequest() { FileName = filePath };
                    var actual = await host.GetResponse<CodeCheckRequest, QuickFixResponse>(OmniSharpEndpoints.CodeCheck, request);
                    Assert.Single(actual.QuickFixes);
                    Assert.Equal("; expected (CS1002)", actual.QuickFixes.First().Text);
                }
            }
        }

        [Fact]
        public async Task Adds_Misc_Document_Which_Supports_Signature_help()
        {
            const string source =
@"class Program
{
    public static void Main(){
        System.Guid.NewGuid($$);
    }
}";
            var testfile = new TestFile("a.cs", source);

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("EmptyProject"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var filePath = await AddTestFile(host, testProject, testfile);
                var point = testfile.Content.GetPointFromPosition();
                var request = new SignatureHelpRequest()
                {
                    FileName = filePath,
                    Line = point.Line,
                    Column = point.Offset,
                    Buffer = testfile.Content.Code
                };

                var actual = await host.GetResponse<SignatureHelpRequest, SignatureHelpResponse>(OmniSharpEndpoints.SignatureHelp, request);
                Assert.Single(actual.Signatures);
                Assert.Equal(0, actual.ActiveParameter);
                Assert.Equal(0, actual.ActiveSignature);
                Assert.Equal("NewGuid", actual.Signatures.ElementAt(0).Name);
                Assert.Empty(actual.Signatures.ElementAt(0).Parameters);
            }
        }

        [Fact]
        public async Task Adds_Misc_Document_Which_Supports_Implementations()
        {
            const string source = @"
                public class MyClass 
                { 
                    public MyClass() { Fo$$o(); }
                    public void Foo() {}
                }";

            var testfile = new TestFile("a.cs", source);

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("EmptyProject"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var filePath = await AddTestFile(host, testProject, testfile);
                var point = testfile.Content.GetPointFromPosition();
                var request = new FindImplementationsRequest()
                {
                    FileName = filePath,
                    Line = point.Line,
                    Column = point.Offset,
                    Buffer = testfile.Content.Code
                };

                var actual = await host.GetResponse<FindImplementationsRequest, QuickFixResponse>(OmniSharpEndpoints.FindImplementations, request);
                Assert.Single(actual.QuickFixes);
                Assert.Equal("public void Foo() {}", actual.QuickFixes.First().Text.Trim());
            }
        }

        [Fact]
        public async Task Adds_Misc_Document_Which_Supports_Usages()
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
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("EmptyProject"))
            {
                using (var host = CreateOmniSharpHost(testProject.Directory))
                {
                    var filePath = await AddTestFile(host, testProject, testfile);
                    var point = testfile.Content.GetPointFromPosition();
                    var request = new FindUsagesRequest()
                    {
                        FileName = filePath,
                        Line = point.Line,
                        Column = point.Offset,
                        Buffer = testfile.Content.Code
                    };

                    var actual = await host.GetResponse<FindUsagesRequest, QuickFixResponse>(OmniSharpEndpoints.FindUsages, request);
                    Assert.Equal(2, actual.QuickFixes.Count());
                }
            }
        }

        [Fact]
        public async Task Adds_Misc_Document_Which_Supports_Symbols()
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
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("EmptyProject"))
            {
                using (var host = CreateOmniSharpHost(testProject.Directory))
                {
                    var filePath = await AddTestFile(host, testProject, testfile);
                    var actual = await host.GetResponse<FindSymbolsRequest, QuickFixResponse>(OmniSharpEndpoints.FindSymbols, null);
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
        public async Task Adds_Misc_Document_Which_Supports_FixUsings()
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
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("EmptyProject"))
            {
                using (var host = CreateOmniSharpHost(testProject.Directory))
                {
                    var filePath = await AddTestFile(host, testProject, testfile);
                    var request = new FixUsingsRequest() { FileName = filePath };
                    var actual = await host.GetResponse<FixUsingsRequest, FixUsingsResponse>(OmniSharpEndpoints.FixUsings, request);
                    Assert.Equal(expectedCode.Replace("\r\n", "\n"), actual.Buffer.Replace("\r\n", "\n"));
                }
            }
        }

        [Fact]
        public async Task Adds_Misc_Document_Which_Supports_TypeLookup()
        {
            const string code = @"class F$$oo {}";

            var testfile = new TestFile("a.cs", code);

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("EmptyProject"))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var filePath = await AddTestFile(host, testProject, testfile);
                var service = host.GetRequestHandler<TypeLookupService>(OmniSharpEndpoints.TypeLookup);
                var point = testfile.Content.GetPointFromPosition();
                var request = new TypeLookupRequest
                {
                    FileName = filePath,
                    Line = point.Line,
                    Column = point.Offset,
                };

                var actual = await host.GetResponse<TypeLookupRequest, TypeLookupResponse>(OmniSharpEndpoints.TypeLookup, request);
                Assert.Equal("Foo", actual.Type);
            }
        }

        [Fact]
        public async Task Adds_Multiple_Misc_Files_To_Same_project()
        {
            const string source1 =
@"class Program
{
    public static void Main(){
        A a = new A(4, $$5);
    }
}";

            const string source2 =
@"class A
{
    A(int a, int b)
    {
    }
}";
            var testfile1 = new TestFile("file1.cs", source1);
            var testfile2 = new TestFile("file2.cs", source2);
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("EmptyProject"))
            {
                using (var host = CreateOmniSharpHost(testProject.Directory))
                {
                    var filePath1 = await AddTestFile(host, testProject, testfile1);
                    var filePath2 = await AddTestFile(host, testProject, testfile2);
                    var point = testfile1.Content.GetPointFromPosition();
                    var request = new SignatureHelpRequest()
                    {
                        FileName = filePath1,
                        Line = point.Line,
                        Column = point.Offset,
                        Buffer = testfile1.Content.Code
                    };

                    var actual = await host.GetResponse<SignatureHelpRequest, SignatureHelpResponse>(OmniSharpEndpoints.SignatureHelp, request);
                    Assert.Single(actual.Signatures);
                    Assert.Equal(1, actual.ActiveParameter);
                    Assert.Equal(0, actual.ActiveSignature);
                    Assert.Equal("A", actual.Signatures.ElementAt(0).Name);
                    Assert.Equal(2, actual.Signatures.ElementAt(0).Parameters.Count());
                }
            }
        }

        [Fact]
        public async Task Handles_Misc_File_Deletion()
        {
            //When the file is deleted the diagnostics must not be returned
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("EmptyProject"))
            {
                var testfile = new TestFile("a.cs", "class C { b a = new b(); int n  }");
                using (var host = CreateOmniSharpHost(testProject.Directory))
                {
                    var filePath = await AddTestFile(host, testProject, testfile);
                    var request = new CodeCheckRequest() { FileName = filePath };
                    var actual = await host.GetResponse<CodeCheckRequest, QuickFixResponse>(OmniSharpEndpoints.CodeCheck, request);
                    Assert.Single(actual.QuickFixes);

                    await WaitForFileUpdate(filePath, host, FileWatching.FileChangeType.Delete);
                    actual = await host.GetResponse<CodeCheckRequest, QuickFixResponse>(OmniSharpEndpoints.CodeCheck, request);
                    Assert.Empty(actual.QuickFixes);
                }
            }
        }

        private async Task<string> AddTestFile(OmniSharpTestHost host, ITestProject testProject, TestFile testfile)
        {
            var filePath = testProject.AddDisposableFile(testfile.FileName, testfile.Content.Text.ToString());
            await host.Workspace.BufferManager.UpdateBufferAsync(new Request() { FileName = filePath, Buffer = testfile.Content.Text.ToString() });
            return filePath;
        }

        private async Task WaitForFileUpdate(string filePath, OmniSharpTestHost host, FileWatching.FileChangeType changeType = FileWatching.FileChangeType.Create)
        {
            var fileChangedService = host.GetRequestHandler<OnFilesChangedService>(OmniSharpEndpoints.FilesChanged);
            await fileChangedService.Handle(new[]
            {
                    new FilesChangedRequest
                    {
                        FileName = filePath,
                        ChangeType = changeType
                    }
                });

            await Task.Delay(2000);
        }
    }
}
