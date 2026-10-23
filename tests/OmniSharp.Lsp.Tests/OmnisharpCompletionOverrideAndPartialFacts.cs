using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Configuration;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using TestUtility;
using Xunit;

namespace OmniSharp.Lsp.Tests
{
    public class OmnisharpCompletionOverrideAndPartialFacts : OmnisharpCompletionTestBase
    {
        public OmnisharpCompletionOverrideAndPartialFacts(ITestOutputHelper output) : base(output)
        {
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_Publics(string filename)
        {
            const string source = @"
class Foo
{
    public virtual void Test(string text) {}
    public virtual void Test(string text, string moreText) {}
}

class FooChild : Foo
{
    override $$
}
";

            var completions = await FindCompletionsAsync(filename, source);
            Assert.Equal(new[] { "Equals(object? obj)", "GetHashCode()", "Test(string text)", "Test(string text, string moreText)", "ToString()" },
                         completions.Items.Select(c => c.Label));
            Assert.Equal(new[] { "public override bool Equals(object obj)\n    {\n        return base.Equals(obj);$0\n    \\}",
                                 "public override int GetHashCode()\n    {\n        return base.GetHashCode();$0\n    \\}",
                                 "public override void Test(string text)\n    {\n        base.Test(text);$0\n    \\}",
                                 "public override void Test(string text, string moreText)\n    {\n        base.Test(text, moreText);$0\n    \\}",
                                 "public override string ToString()\n    {\n        return base.ToString();$0\n    \\}"
                                },
                         completions.Items.Select(c => c.TextEdit.TextEdit.NewText));

            Assert.Equal(new[] { "override Equals",
                                 "override GetHashCode",
                                 "override Test",
                                 "override Test",
                                 "override ToString"
                                },
                        completions.Items.Select(c => c.FilterText));

            Assert.All(completions.Items, c => Assert.Null(c.AdditionalTextEdits));

            Assert.All(completions.Items.Select(c => c.TextEdit.TextEdit),
                       r =>
                       {
                           Assert.Equal(9, r.Range.Start.Line);
                           Assert.Equal(4, r.Range.Start.Character);
                           Assert.Equal(9, r.Range.End.Line);
                           Assert.Equal(13, r.Range.End.Character);
                       });

            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_UnimportedTypesFullyQualified(string filename)
        {
            const string source = @"
using N2;
namespace N1
{
    public class CN1 {}
}
namespace N2
{
    using N1;
    public abstract class IN2 { protected abstract CN1 GetN1(); }
}
namespace N3
{
    class CN3 : IN2
    {
        override $$
    }
}";

            var completions = await FindCompletionsAsync(filename, source);
            Assert.Equal(new[] { "Equals(object? obj)", "GetHashCode()", "GetN1()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "public override bool Equals(object obj)\n        {\n            return base.Equals(obj);$0\n        \\}",
                                 "public override int GetHashCode()\n        {\n            return base.GetHashCode();$0\n        \\}",
                                 "protected override N1.CN1 GetN1()\n        {\n            throw new System.NotImplementedException();$0\n        \\}",
                                 "public override string ToString()\n        {\n            return base.ToString();$0\n        \\}"
                               },
                         completions.Items.Select(c => c.TextEdit.TextEdit.NewText));

            Assert.Equal(new[] { "override Equals",
                                 "override GetHashCode",
                                 "override GetN1",
                                 "override ToString"
                               },
                        completions.Items.Select(c => c.FilterText));

            Assert.All(completions.Items.Select(c => c.TextEdit.TextEdit),
                       r =>
                       {
                           Assert.Equal(15, r.Range.Start.Line);
                           Assert.Equal(8, r.Range.Start.Character);
                           Assert.Equal(15, r.Range.End.Line);
                           Assert.Equal(17, r.Range.End.Character);
                       });

            Assert.All(completions.Items, c => Assert.Null(c.AdditionalTextEdits));

            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_ModifierInFront(string filename)
        {
            const string source = @"
class C
{
    public override $$
}";

            var completions = await FindCompletionsAsync(filename, source);
            Assert.Equal(new[] { "Equals(object? obj)", "GetHashCode()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "bool Equals(object obj)\n    {\n        return base.Equals(obj);$0\n    \\}",
                                 "int GetHashCode()\n    {\n        return base.GetHashCode();$0\n    \\}",
                                 "string ToString()\n    {\n        return base.ToString();$0\n    \\}"
                               },
                         completions.Items.Select(c => c.TextEdit.TextEdit.NewText));

            Assert.Equal(new[] { "Equals",
                                 "GetHashCode",
                                 "ToString"
                               },
                         completions.Items.Select(c => c.FilterText));

            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), a => Assert.Null(a));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_ModifierAndReturnTypeInFront(string filename)
        {
            const string source = @"
class C
{
    public override bool $$
}";

            var completions = await FindCompletionsAsync(filename, source);
            Assert.Equal(new[] { "Equals(object? obj)" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "Equals(object obj)\n    {\n        return base.Equals(obj);$0\n    \\}" },
                         completions.Items.Select(c => c.TextEdit.TextEdit.NewText));

            Assert.Equal(new[] { "Equals" },
                         completions.Items.Select(c => c.FilterText));


            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), a => Assert.Null(a));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_TestTest(string filename)
        {
            const string source = @"
class Test {}
abstract class Base
{
    protected abstract Test Test();
}
class Derived : Base
{
    override $$
}";

            var completions = await FindCompletionsAsync(filename, source);
            Assert.Equal(new[] { "Equals(object? obj)", "GetHashCode()", "Test()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "public override bool Equals(object obj)\n    {\n        return base.Equals(obj);$0\n    \\}",
                                 "public override int GetHashCode()\n    {\n        return base.GetHashCode();$0\n    \\}",
                                 "protected override Test Test()\n    {\n        throw new System.NotImplementedException();$0\n    \\}",
                                 "public override string ToString()\n    {\n        return base.ToString();$0\n    \\}"
                               },
                         completions.Items.Select(c => c.TextEdit.TextEdit.NewText));

            Assert.Equal(new[] { "override Equals",
                                 "override GetHashCode",
                                 "override Test",
                                 "override ToString"
                               },
                        completions.Items.Select(c => c.FilterText));

            Assert.All(completions.Items.Select(c => c.TextEdit.TextEdit),
                       r =>
                       {
                           Assert.Equal(8, r.Range.Start.Line);
                           Assert.Equal(4, r.Range.Start.Character);
                           Assert.Equal(8, r.Range.End.Line);
                           Assert.Equal(13, r.Range.End.Character);
                       });

            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
        }

        [Fact]
        public async Task OverrideCompletion_TypesNeedImport()
        {
            const string baseText = @"
using System;
public class Base
{
    public virtual Action GetAction(Action a) => null;
}
";

            const string derivedText = @"
public class Derived : Base
{
    override $$
}";

            var completions = await FindCompletionsAsync("derived.cs", derivedText, additionalFiles: new[] { new TestFile("base.cs", baseText) });
            var item = completions.Items.Single(c => c.Label.StartsWith("GetAction"));
            Assert.Equal("GetAction(System.Action a)", item.Label);

            Assert.Single(item.AdditionalTextEdits);
            Assert.Equal(NormalizeNewlines("using System;\n\n"), item.AdditionalTextEdits.First().NewText);
            Assert.Equal(1, item.AdditionalTextEdits.First().Range.Start.Line);
            Assert.Equal(0, item.AdditionalTextEdits.First().Range.Start.Character);
            Assert.Equal(1, item.AdditionalTextEdits.First().Range.End.Line);
            Assert.Equal(0, item.AdditionalTextEdits.First().Range.End.Character);
            Assert.Equal("public override Action GetAction(Action a)\n    {\n        return base.GetAction(a);$0\n    \\}", item.TextEdit.TextEdit.NewText);
            Assert.Equal(3, item.TextEdit.TextEdit.Range.Start.Line);
            Assert.Equal(4, item.TextEdit.TextEdit.Range.Start.Character);
            Assert.Equal(3, item.TextEdit.TextEdit.Range.End.Line);
            Assert.Equal(13, item.TextEdit.TextEdit.Range.End.Character);
            Assert.Equal("override GetAction", item.FilterText);
        }

        [Fact]
        public async Task OverrideCompletion_FromNullableToNonNullableContext()
        {
            const string text = @"
#nullable enable
public class Base
{
    public virtual object? M1(object? param) => throw null;
}
#nullable disable
public class Derived : Base
{
    override $$
}";

            var completions = await FindCompletionsAsync("derived.cs", text);
            var item = completions.Items.Single(c => c.Label.StartsWith("M1"));
            Assert.Equal("M1(object? param)", item.Label);

            Assert.Null(item.AdditionalTextEdits);
            Assert.Equal(9, item.TextEdit.TextEdit.Range.Start.Line);
            Assert.Equal(4, item.TextEdit.TextEdit.Range.Start.Character);
            Assert.Equal(9, item.TextEdit.TextEdit.Range.End.Line);
            Assert.Equal(13, item.TextEdit.TextEdit.Range.End.Character);
            Assert.Equal("public override object M1(object param)\n    {\n        return base.M1(param);$0\n    \\}", item.TextEdit.TextEdit.NewText);
            Assert.Equal("override M1", item.FilterText);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideCompletion_PropertyGetSet(string filename)
        {
            const string source = @"
using System;
public class Base
{
    public abstract string Prop { get; set; }
}
public class Derived : Base
{
    override $$
}
";

            var completions = await FindCompletionsAsync(filename, source);
            var item = completions.Items.Single(c => c.Label.StartsWith("Prop"));
            Assert.Equal("Prop", item.Label);

            Assert.Null(item.AdditionalTextEdits);
            Assert.Equal(8, item.TextEdit.TextEdit.Range.Start.Line);
            Assert.Equal(4, item.TextEdit.TextEdit.Range.Start.Character);
            Assert.Equal(8, item.TextEdit.TextEdit.Range.End.Line);
            Assert.Equal(13, item.TextEdit.TextEdit.Range.End.Character);
            Assert.Equal("public override string Prop { get => throw new NotImplementedException()$0; set => throw new NotImplementedException(); \\}", item.TextEdit.TextEdit.NewText);
            Assert.Equal("override Prop", item.FilterText);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideCompletion_PropertyGet(string filename)
        {
            const string source = @"
using System;
public class Base
{
    public abstract string Prop { get; }
}
public class Derived : Base
{
    override $$
}
";

            var completions = await FindCompletionsAsync(filename, source);
            var item = completions.Items.Single(c => c.Label.StartsWith("Prop"));
            Assert.Equal("Prop", item.Label);

            Assert.Null(item.AdditionalTextEdits);
            Assert.Equal(8, item.TextEdit.TextEdit.Range.Start.Line);
            Assert.Equal(4, item.TextEdit.TextEdit.Range.Start.Character);
            Assert.Equal(8, item.TextEdit.TextEdit.Range.End.Line);
            Assert.Equal(13, item.TextEdit.TextEdit.Range.End.Character);
            Assert.Equal("public override string Prop => throw new NotImplementedException()$0;", item.TextEdit.TextEdit.NewText);
            Assert.Equal(InsertTextFormat.Snippet, item.InsertTextFormat);
            Assert.Equal("override Prop", item.FilterText);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task PartialCompletion(string filename)
        {
            const string source = @"
partial class C
{
    partial void M1(string param);
}
partial class C
{
    partial $$
}
";

            var completions = await FindCompletionsAsync(filename, source);
            Assert.Equal(new[] { "M1(string param)" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "void M1(string param)\n    {\n        throw new System.NotImplementedException();$0\n    \\}" },
                         completions.Items.Select(c => c.TextEdit.TextEdit.NewText));

            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), a => Assert.Null(a));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
        }

        [Fact]
        public async Task PartialCompletion_TypesNeedImport()
        {
            const string file1 = @"
using System;
public partial class C
{
    partial void M(Action a);
}
";

            const string file2 = @"
public partial class C
{
    partial $$
}";

            var completions = await FindCompletionsAsync("derived.cs", file2, additionalFiles: new[] { new TestFile("base.cs", file1) });
            var item = completions.Items.Single(c => c.Label.StartsWith("M"));

            Assert.Single(item.AdditionalTextEdits);
            Assert.Equal(NormalizeNewlines("using System;\n\n"), item.AdditionalTextEdits.First().NewText);
            Assert.Equal(1, item.AdditionalTextEdits.First().Range.Start.Line);
            Assert.Equal(0, item.AdditionalTextEdits.First().Range.Start.Character);
            Assert.Equal(1, item.AdditionalTextEdits.First().Range.End.Line);
            Assert.Equal(0, item.AdditionalTextEdits.First().Range.End.Character);
            Assert.Equal("void M(Action a)\n    {\n        throw new NotImplementedException();$0\n    \\}", item.TextEdit.TextEdit.NewText);
            Assert.Equal("M", item.FilterText);
            Assert.Equal(3, item.TextEdit.TextEdit.Range.Start.Line);
            Assert.Equal(12, item.TextEdit.TextEdit.Range.Start.Character);
            Assert.Equal(3, item.TextEdit.TextEdit.Range.End.Line);
            Assert.Equal(12, item.TextEdit.TextEdit.Range.End.Character);
        }

        [Fact]
        public async Task PartialCompletion_FromNullableToNonNullableContext()
        {
            const string text = @"
#nullable enable
public partial class C
{
    partial void M1(object? param);
}
#nullable disable
public partial class C
{
    partial $$
}";

            var completions = await FindCompletionsAsync("derived.cs", text);
            var item = completions.Items.Single(c => c.Label.StartsWith("M1"));
            Assert.Equal("M1(object param)", item.Label);
            Assert.Null(item.AdditionalTextEdits);
            Assert.Equal("void M1(object param)\n    {\n        throw new System.NotImplementedException();$0\n    \\}", item.TextEdit.TextEdit.NewText);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_PartiallyTypedIdentifier(string filename)
        {
            const string source = @"
class C
{
    override Ge$$
}";

            var completions = await FindCompletionsAsync(filename, source);
            Assert.Equal(new[] { "Equals(object? obj)", "GetHashCode()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "public override bool Equals(object obj)\n    {\n        return base.Equals(obj);$0\n    \\}",
                                 "public override int GetHashCode()\n    {\n        return base.GetHashCode();$0\n    \\}",
                                 "public override string ToString()\n    {\n        return base.ToString();$0\n    \\}"
                               },
                         completions.Items.Select(c => c.TextEdit.TextEdit.NewText));

            Assert.Equal(new[] { "override Equals",
                                 "override GetHashCode",
                                 "override ToString"
                               },
                        completions.Items.Select(c => c.FilterText));

            Assert.All(completions.Items, c => Assert.Null(c.AdditionalTextEdits));

            Assert.All(completions.Items.Select(c => c.TextEdit.TextEdit),
                       r =>
                       {
                           Assert.Equal(3, r.Range.Start.Line);
                           Assert.Equal(4, r.Range.Start.Character);
                           Assert.Equal(3, r.Range.End.Line);
                           Assert.Equal(15, r.Range.End.Character);
                       });

            Assert.All(completions.Items, c => Assert.False(c.Preselect));

            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
        }
    }
}
