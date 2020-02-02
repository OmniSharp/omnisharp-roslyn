using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Models.V2;
using OmniSharp.Models.V2.CodeActions;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class CodeActionsWithOptionsFacts : AbstractCodeActionsTestFixture
    {
        public CodeActionsWithOptionsFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task Can_generate_constructor_with_default_arguments()
        {
            const string code =
                @"public class Class1[||]
                {
                    public string PropertyHere { get; set; }
                }";
            const string expected =
                @"public class Class1
                {
                    public Class1(string propertyHere)
                    {
                        PropertyHere = propertyHere;
                    }

                    public string PropertyHere { get; set; }
                }
                ";
            var response = await RunRefactoringAsync(code, "Generate constructor...");
            AssertIgnoringIndent(expected, ((ModifiedFileResponse)response.Changes.First()).Buffer);
        }

        [Fact]
        public async Task Can_generate_overrides()
        {
            const string code =
                @"public class Class1[||]
                {
                }";
            const string expected =
                @"public class Class1
                {
                    public override bool Equals(object obj)
                    {
                        return base.Equals(obj);
                    }

                    public override int GetHashCode()
                    {
                        return base.GetHashCode();
                    }

                    public override string ToString()
                    {
                        return base.ToString();
                    }
                }
                ";
            var response = await RunRefactoringAsync(code, "Generate overrides...");
            AssertIgnoringIndent(expected, ((ModifiedFileResponse)response.Changes.First()).Buffer);
        }

        [Fact]
        public async Task Can_generate_equals_for_object()
        {
            const string code =
                @"public class Class1[||]
                {
                    public string PropertyHere { get; set; }
                }";
            const string expected =
                @"public class Class1
                {
                    public string PropertyHere { get; set; }

                    public override bool Equals(object obj)
                    {
                        return obj is Class1 @class &&
                               PropertyHere == @class.PropertyHere;
                    }
                }
                ";
            var response = await RunRefactoringAsync(code, "Generate Equals(object)...");
            AssertIgnoringIndent(expected, ((ModifiedFileResponse)response.Changes.First()).Buffer);
        }

        [Fact]
        public async Task Can_generate_equals_and_hashcode_for_object()
        {
            const string code =
                @"public class Class1[||]
                {
                    public string PropertyHere { get; set; }
                }";

            var response = await RunRefactoringAsync(code, "Generate Equals and GetHashCode...");

            // This doesn't check exact class form because different framework version implement hashcode differently.
            Assert.Contains("public override int GetHashCode()", ((ModifiedFileResponse)response.Changes.First()).Buffer);
            Assert.Contains("public override bool Equals(object obj)", ((ModifiedFileResponse)response.Changes.First()).Buffer);
        }

        [Fact]
        // How to implement this witout proper UI?
        public async Task Blacklists_Change_signature()
        {
            const string code =
                @"public class Class1
                {
                    public void Foo(string a[||], string b)
                    {
                    }
                }";

            var response = await FindRefactoringNamesAsync(code);

            Assert.DoesNotContain("Change signature...", response);
        }

        [Fact]
        // Theres generate type without options that gives ~same result with default ui. No point to bring this.
        public async Task Blacklists_generate_type_with_UI()
        {
            const string code =
                @"public class Class1: NonExistentBaseType[||]
                {
                }";

            var response = await FindRefactoringNamesAsync(code);

            Assert.DoesNotContain("Generate type 'NonExistentBaseType' -> Generate new type...", response);
        }

        [Fact]
        // Theres generate type without options that gives ~same result with default ui. No point to bring this.
        public async Task Blacklists_pull_members_up_with_UI()
        {
            const string code =
                @"
                public class Class1: BaseClass
                {
                    public string Foo[||] { get; set; }
                }

                public class BaseClass {}
";

            var response = await FindRefactoringNamesAsync(code);

            Assert.DoesNotContain("Pull 'Foo' up -> Pull members up to base type...", response);
        }

        [Fact]
        public async Task Can_extract_interface()
        {
            const string code =
                @"public class Class1[||]
                {
                    public string PropertyHere { get; set; }
                }";

            const string expected =
                @"
                public interface IClass1
                {
                    string PropertyHere { get; set; }
                }

                public class Class1 : IClass1
                {
                    public string PropertyHere { get; set; }
                }
                ";
            var response = await RunRefactoringAsync(code, "Extract interface...");

            AssertIgnoringIndent(expected, ((ModifiedFileResponse)response.Changes.First()).Buffer);
        }

        [Fact]
        public void Check_better_alternative_available_for_codeaction_with_options()
        {
            // This keeps record is Microsoft.CodeAnalysis.PickMembers.IPickMembersService public or not
            // and should fail on case where interface is exposed. Which likely means that this is officially
            // supported scenario by roslyn.
            //
            // In case it's exposed by roslyn team these services can be simplified.
            // Steps are likely following:
            // - Remove proxies
            // - Replace ExportWorkspaceServiceFactoryWithAssemblyQualifiedName with Microsoft.CodeAnalysis.Host.Mef.ExportWorkspaceServiceAttribute
            // - Fix proxy classes to implement IPickMembersService / IExtractInterfaceOptionsService ... instead of proxy and reflection.
            // - Remove all factories using ExportWorkspaceServiceFactoryWithAssemblyQualifiedName and factory itself.
            // Following issue may have additional information: https://github.com/dotnet/roslyn/issues/33277
            var pickMemberServiceType = Assembly.Load("Microsoft.CodeAnalysis.Features").GetType("Microsoft.CodeAnalysis.PickMembers.IPickMembersService");
            Assert.False(pickMemberServiceType.IsPublic);
        }
    }
}
