using System;
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
            string lineEnding = Environment.NewLine;
            //VS Code renders a single new line for two newline sequences in the response, hence two new lines are passed in the lineEnding parameter 
            var plainText = DocumentationConverter.ConvertDocumentation(documentation,lineEnding+lineEnding);
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
            //To remove the extra new lines added for VS Code for comparison, Replace function is being used
            plainText = plainText.Replace(lineEnding + lineEnding, lineEnding);
            Assert.Equal(expected, plainText, ignoreLineEndingDifferences: true);
        }

        [Fact]
        public void Has_correct_spacing_around_paramref()
        {
            var documentation = @"
<summary>DoWork is a method in the TestClass class.
The <paramref name=""arg""/> parameter takes a number and <paramref name=""arg2""/> takes a string.
</summary>";
            string lineEnding = Environment.NewLine;
            var plainText = DocumentationConverter.ConvertDocumentation(documentation, lineEnding+lineEnding);
            var expected =
@"DoWork is a method in the TestClass class.
The arg parameter takes a number and arg2 takes a string.";
            plainText = plainText.Replace(lineEnding+lineEnding,lineEnding);
            Assert.Equal(expected, plainText, ignoreLineEndingDifferences: true);
        }
    }
}
