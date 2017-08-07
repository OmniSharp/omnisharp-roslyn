using System.Threading.Tasks;
using OmniSharp.Models.TypeLookup;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Types;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class TypeLookupFacts : AbstractSingleRequestHandlerTestFixture<TypeLookupService>
    {
        public TypeLookupFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.TypeLookup;

        [Fact]
        public async Task OmitsNamespaceForNonRegularCSharpSyntax()
        {
            var source = @"class Foo {}";

            var testFile = new TestFile("dummy.csx", source);
            var workspace = TestHelpers.CreateCsxWorkspace(testFile);

            var controller = new TypeLookupService(workspace, new FormattingOptions());
            var response = await controller.Handle(new TypeLookupRequest { FileName = testFile.FileName, Line = 0, Column = 7 });

            Assert.Equal("Foo", response.Type);
        }

        [Fact]
        public async Task OmitsNamespaceForTypesInGlobalNamespace()
        {
            const string source = @"namespace Bar {
            class Foo {}
            }
            class Baz {}";

            var testFile = new TestFile("dummy.cs", source);
            using (var host = CreateOmniSharpHost(testFile))
            {
                var requestHandler = GetRequestHandler(host);

                var requestInNormalNamespace = new TypeLookupRequest { FileName = testFile.FileName, Line = 1, Column = 19 };
                var responseInNormalNamespace = await requestHandler.Handle(requestInNormalNamespace);

                var requestInGlobalNamespace = new TypeLookupRequest { FileName = testFile.FileName, Line = 3, Column = 19 };
                var responseInGlobalNamespace = await requestHandler.Handle(requestInGlobalNamespace);

                Assert.Equal("Bar.Foo", responseInNormalNamespace.Type);
                Assert.Equal("Baz", responseInGlobalNamespace.Type);
            }
        }

        [Fact]
        public async Task IncludesNamespaceForRegularCSharpSyntax()
        {
            const string source = @"namespace Bar {
            class Foo {}
            }";

            var testFile = new TestFile("dummy.cs", source);
            using (var host = CreateOmniSharpHost(testFile))
            {
                var requestHandler = GetRequestHandler(host);

                var request = new TypeLookupRequest { FileName = testFile.FileName, Line = 1, Column = 19 };
                var response = await requestHandler.Handle(request);

                Assert.Equal("Bar.Foo", response.Type);
            }
        }

        [Fact]
        public async Task IncludesContainingTypeFoNestedTypesForRegularCSharpSyntax()
        {
            var source = @"namespace Bar {
            class Foo {
                    class Xyz {}
                }   
            }";

            var testFile = new TestFile("dummy.cs", source);
            using (var host = CreateOmniSharpHost(testFile))
            {
                var requestHandler = GetRequestHandler(host);

                var request = new TypeLookupRequest { FileName = testFile.FileName, Line = 2, Column = 27 };
                var response = await requestHandler.Handle(request);

                Assert.Equal("Bar.Foo.Xyz", response.Type);
            }
        }

        [Fact]
        public async Task IncludesContainingTypeFoNestedTypesForNonRegularCSharpSyntax()
        {
            var source = @"class Foo {
                class Bar {}
            }";

            var testFile = new TestFile("dummy.csx", source);
            var workspace = TestHelpers.CreateCsxWorkspace(testFile);

            var controller = new TypeLookupService(workspace, new FormattingOptions());
            var request = new TypeLookupRequest { FileName = testFile.FileName, Line = 1, Column = 23 };
            var response = await controller.Handle(request);

            Assert.Equal("Foo.Bar", response.Type);
        }

        private static TestFile s_testFile = new TestFile("dummy.cs",
            @"using System;
            using Bar2;
            using System.Collections.Generic;
            namespace Bar {
                class Foo {
                    public Foo() {
                        Console.WriteLine(""abc"");
                    }

                    public void MyMethod(string name, Foo foo, Foo2 foo2) { };

                    private Foo2 _someField = new Foo2();

                    public Foo2 SomeProperty { get; }

                    public IDictionary<string, IEnumerable<int>> SomeDict { get; }

                    public void Compute(int index = 2) { }
                }
            }

            namespace Bar2 {
                class Foo2 {
                }
            }
            ");

        private async Task<TypeLookupResponse> GetTypeLookUpResponse(int line, int column)
        {
            using (var host = CreateOmniSharpHost(s_testFile))
            {
                var requestHandler = GetRequestHandler(host);
                var request = new TypeLookupRequest { FileName = s_testFile.FileName, Line = line, Column = column };

                return await requestHandler.Handle(request);
            }
        }

        [Fact]
        public async Task DisplayFormatForMethodSymbol_Invocation()
        {
            var response = await GetTypeLookUpResponse(line: 6, column: 35);

            Assert.Equal("void Console.WriteLine(string value)", response.Type);
        }

        [Fact]
        public async Task DisplayFormatForMethodSymbol_Declaration()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 35);
            Assert.Equal("void Foo.MyMethod(string name, Foo foo, Foo2 foo2)", response.Type);
        }

        [Fact]
        public async Task DisplayFormatFor_TypeSymbol_Primitive()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 46);
            Assert.Equal("System.String", response.Type);
        }

        [Fact]
        public async Task DisplayFormatFor_TypeSymbol_ComplexType_SameNamespace()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 56);
            Assert.Equal("Bar.Foo", response.Type);
        }

        [Fact]
        public async Task DisplayFormatFor_TypeSymbol_ComplexType_DifferentNamespace()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 67);
            Assert.Equal("Bar2.Foo2", response.Type);
        }

        [Fact]
        public async Task DisplayFormatFor_TypeSymbol_WithGenerics()
        {
            var response = await GetTypeLookUpResponse(line: 15, column: 36);
            Assert.Equal("System.Collections.Generic.IDictionary<System.String, System.Collections.Generic.IEnumerable<System.Int32>>", response.Type);
        }

        [Fact]
        public async Task DisplayFormatForParameterSymbol_Name_Primitive()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 51);
            Assert.Equal("string name", response.Type);
        }

        [Fact]
        public async Task DisplayFormatFor_ParameterSymbol_ComplexType_SameNamespace()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 60);
            Assert.Equal("Foo foo", response.Type);
        }

        [Fact]
        public async Task DisplayFormatFor_ParameterSymbol_Name_ComplexType_DifferentNamespace()
        {
            var response = await GetTypeLookUpResponse(line: 9, column: 71);
            Assert.Equal("Foo2 foo2", response.Type);
        }

        [Fact]
        public async Task DisplayFormatFor_ParameterSymbol_Name_WithDefaultValue()
        {
            var response = await GetTypeLookUpResponse(line: 17, column: 48);
            Assert.Equal("int index = 2", response.Type);
        }

        [Fact]
        public async Task DisplayFormatFor_FieldSymbol()
        {
            var response = await GetTypeLookUpResponse(line: 11, column: 38);
            Assert.Equal("Foo2 Foo._someField", response.Type);
        }

        [Fact]
        public async Task DisplayFormatFor_PropertySymbol()
        {
            var response = await GetTypeLookUpResponse(line: 13, column: 38);
            Assert.Equal("Foo2 Foo.SomeProperty", response.Type);
        }

        [Fact]
        public async Task DisplayFormatFor_PropertySymbol_WithGenerics()
        {
            var response = await GetTypeLookUpResponse(line: 15, column: 70);
            Assert.Equal("IDictionary<string, IEnumerable<int>> Foo.SomeDict", response.Type);
        }
    }
}
