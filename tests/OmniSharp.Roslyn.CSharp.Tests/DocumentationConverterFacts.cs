using OmniSharp.Roslyn.CSharp.Services.Documentation;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class DocumentationConverterFacts
    {
        [Fact]
        public void Converts_xml_documentation_to_plain_text()
        {
            var documentation = @"
<member name=""M:TestNamespace.TestClass.GetZero"">
    <summary>
    The GetZero method.
    </summary>
    <example>
    This sample shows how to call the <see cref=""M:TestNamespace.TestClass.GetZero""/> method.
    <code>
    class TestClass
    {
        static int Main()
        {
            return GetZero();
        }
    }
    </code>
    </example>
</member>";
            var plainText = DocumentationConverter.ConvertDocumentation(documentation, "\n");
            var expected =
@"The GetZero method.

Example:
This sample shows how to call the TestNamespace.TestClass.GetZero method.

    class TestClass
    {
        static int Main()
        {
            return GetZero();
        }
    }
    ";
            Assert.Equal(expected, plainText, ignoreLineEndingDifferences: true);
        }

        [Fact]
        public void Has_correct_spacing_around_paramref()
        {
            var documentation = @"
<summary>DoWork is a method in the TestClass class.
The <paramref name=""arg""/> parameter takes a number and <paramref name=""arg2""/> takes a string.
</summary>";
            var plainText = DocumentationConverter.ConvertDocumentation(documentation, "\n");
            var expected =
@"DoWork is a method in the TestClass class.
The arg parameter takes a number and arg2 takes a string.";
            Assert.Equal(expected, plainText, ignoreLineEndingDifferences: true);
        }

        [Fact]
        public void Has_typeparam_and_param_in_description()
        {
            var documentation = @"
<member name=""M:TestNamespace.TestClass.CreateWorkspace`1"">
    <summary>
    Creates a workspace.
    </summary>
    <typeparam name=""T"">The type of workspace being created.</typeparam>
    <param name=""Path"">The path to the workspace.</param>
</member>";
            var plainText = DocumentationConverter.ConvertDocumentation(documentation, "\n");
            var expected =
@"Creates a workspace.

<T>: The type of workspace being created.
Path: The path to the workspace.";
            Assert.Equal(expected, plainText, ignoreLineEndingDifferences: true);
        }
    }
}
