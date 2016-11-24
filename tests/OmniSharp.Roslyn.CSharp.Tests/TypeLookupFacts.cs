using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Types;
using TestUtility;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class TypeLookupFacts
    {
        [Fact]
        public async Task OmitsNamespaceForNonRegularCSharpSyntax()
        {
            var source1 = @"class Foo {}";

            var workspace = TestHelpers.CreateCsxWorkspace(source1);

            var controller = new TypeLookupService(workspace, new FormattingOptions());
            var response = await controller.Handle(new TypeLookupRequest { FileName = "dummy.csx", Line = 0, Column = 7 });

            Assert.Equal("Foo", response.Type);
        }

        [Fact]
        public async Task OmitsNamespaceForTypesInGlobalNamespace()
        {
            var source = @"namespace Bar {
            class Foo {}
            }
            class Baz {}";

            var workspace = await TestHelpers.CreateSimpleWorkspace(source);

            var controller = new TypeLookupService(workspace, new FormattingOptions());
            var responseInNormalNamespace = await controller.Handle(new TypeLookupRequest { FileName = "dummy.cs", Line = 1, Column = 19 });
            var responseInGlobalNamespace = await controller.Handle(new TypeLookupRequest { FileName = "dummy.cs", Line = 3, Column = 19 });

            Assert.Equal("Bar.Foo", responseInNormalNamespace.Type);
            Assert.Equal("Baz", responseInGlobalNamespace.Type);
        }

        [Fact]
        public async Task IncludesNamespaceForRegularCSharpSyntax()
        {
            var source1 = @"namespace Bar {
            class Foo {}
            }";

            var workspace = await TestHelpers.CreateSimpleWorkspace(source1);

            var controller = new TypeLookupService(workspace, new FormattingOptions());
            var response = await controller.Handle(new TypeLookupRequest { FileName = "dummy.cs", Line = 1, Column = 19 });

            Assert.Equal("Bar.Foo", response.Type);
        }

        [Fact]
        public async Task IncludesContainingTypeFoNestedTypesForRegularCSharpSyntax()
        {
            var source1 = @"namespace Bar {
            class Foo {
                    class Xyz {}
                }   
            }";

            var workspace = await TestHelpers.CreateSimpleWorkspace(source1);

            var controller = new TypeLookupService(workspace, new FormattingOptions());
            var response = await controller.Handle(new TypeLookupRequest { FileName = "dummy.cs", Line = 2, Column = 27 });

            Assert.Equal("Bar.Foo.Xyz", response.Type);
        }

        [Fact]
        public async Task IncludesContainingTypeFoNestedTypesForNonRegularCSharpSyntax()
        {
            var source1 = @"class Foo {
                class Bar {}
            }";

            var workspace = TestHelpers.CreateCsxWorkspace(source1);

            var controller = new TypeLookupService(workspace, new FormattingOptions());
            var response = await controller.Handle(new TypeLookupRequest { FileName = "dummy.csx", Line = 1, Column = 23 });

            Assert.Equal("Foo.Bar", response.Type);
        }

        private static string testFile = @"using System;
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
            ";

        private static async Task<TypeLookupResponse> GetTypeLookUpResponse(int line, int column)
        {
            var workspace = await TestHelpers.CreateSimpleWorkspace(testFile);

            var controller = new TypeLookupService(workspace, new FormattingOptions());
            var response = await controller.Handle(new TypeLookupRequest { FileName = "dummy.cs", Line = line, Column = column });

            return response;
        }

        [Fact]
        public async Task DisplayFormatForMethodSymbol_Invocation()
        {
            var response = await GetTypeLookUpResponse(line: 6, column: 35);
            Assert.Equal("void Console.WriteLine(string s)", response.Type);
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
