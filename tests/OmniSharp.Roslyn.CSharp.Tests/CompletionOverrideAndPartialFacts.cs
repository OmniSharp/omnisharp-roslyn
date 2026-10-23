using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Models.v1.Completion;
using OmniSharp.Roslyn.CSharp.Services.Completion;
using TestUtility;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class CompletionOverrideAndPartialFacts : CompletionTestBase
    {
        public CompletionOverrideAndPartialFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_Publics_Async(string filename)
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

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync(filename, source, host);
            Assert.Equal(new[] { "Equals(object? obj)", "GetHashCode()", "Test(string text)", "Test(string text, string moreText)", "ToString()" },
                         completions.Items.Select(c => c.Label));
            Assert.Equal(new[] { "Equals", "GetHashCode", "Test", "Test", "ToString" },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), edit => Assert.Null(edit));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.PlainText, c.InsertTextFormat));

            var afterInsert = await AfterInsertResponse(completions.Items[0], host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("public override bool Equals(object obj)\n    {\n        return base.Equals(obj);\n    }", change.NewText);
            Assert.Equal(8, change.StartLine);
            Assert.Equal(4, change.StartColumn);
            Assert.Equal(8, change.EndLine);
            Assert.Equal(19, change.EndColumn);
            Assert.Equal(10, afterInsert.Line);
            Assert.Equal(32, afterInsert.Column);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_Publics_Sync(string filename)
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

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Equal(new[] { "Equals(object? obj)", "GetHashCode()", "Test(string text)", "Test(string text, string moreText)", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "public override bool Equals(object obj)\n    {\n        return base.Equals(obj);$0\n    \\}",
                                 "public override int GetHashCode()\n    {\n        return base.GetHashCode();$0\n    \\}",
                                 "public override void Test(string text)\n    {\n        base.Test(text);$0\n    \\}",
                                 "public override void Test(string text, string moreText)\n    {\n        base.Test(text, moreText);$0\n    \\}",
                                 "public override string ToString()\n    {\n        return base.ToString();$0\n    \\}"
                                },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.Equal(new[] { "override Equals",
                                 "override GetHashCode",
                                 "override Test",
                                 "override Test",
                                 "override ToString"
                                },
                         completions.Items.Select(c => c.FilterText));

            Assert.All(completions.Items, c => Assert.Null(c.AdditionalTextEdits));

            Assert.All(completions.Items,
                       c =>
                       {
                           Assert.Equal(9, c.TextEdit.StartLine);
                           Assert.Equal(4, c.TextEdit.StartColumn);
                           Assert.Equal(9, c.TextEdit.EndLine);
                           Assert.Equal(13, c.TextEdit.EndColumn);
                       });

            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_UnimportedTypesFullyQualified_Async(string filename)
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

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync(filename, source, host);
            Assert.Equal(new[] { "Equals(object? obj)", "GetHashCode()", "GetN1()", "ToString()" },
                         completions.Items.Select(c => c.Label));
            Assert.Equal(new[] { "Equals", "GetHashCode", "GetN1", "ToString" },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), edit => Assert.Null(edit));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.PlainText, c.InsertTextFormat));

            var afterInsert = await AfterInsertResponse(completions.Items.First(i => i.Label == "GetN1()"), host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("protected override N1.CN1 GetN1()\n        {\n            throw new System.NotImplementedException();\n        }",
                         change.NewText);
            Assert.Equal(15, change.StartLine);
            Assert.Equal(8, change.StartColumn);
            Assert.Equal(15, change.EndLine);
            Assert.Equal(22, change.EndColumn);
            Assert.Equal(17, afterInsert.Line);
            Assert.Equal(55, afterInsert.Column);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_UnimportedTypesFullyQualified_Sync(string filename)
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

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Equal(new[] { "Equals(object? obj)", "GetHashCode()", "GetN1()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "public override bool Equals(object obj)\n        {\n            return base.Equals(obj);$0\n        \\}",
                                 "public override int GetHashCode()\n        {\n            return base.GetHashCode();$0\n        \\}",
                                 "protected override N1.CN1 GetN1()\n        {\n            throw new System.NotImplementedException();$0\n        \\}",
                                 "public override string ToString()\n        {\n            return base.ToString();$0\n        \\}"
                               },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.Equal(new[] { "override Equals",
                                 "override GetHashCode",
                                 "override GetN1",
                                 "override ToString"
                               },
                         completions.Items.Select(c => c.FilterText));

            Assert.All(completions.Items,
                       c =>
                       {
                           Assert.Equal(15, c.TextEdit.StartLine);
                           Assert.Equal(8, c.TextEdit.StartColumn);
                           Assert.Equal(15, c.TextEdit.EndLine);
                           Assert.Equal(17, c.TextEdit.EndColumn);
                       });

            Assert.All(completions.Items, c => Assert.Null(c.AdditionalTextEdits));

            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_ModifierInFront_Async(string filename)
        {
            const string source = @"
class C
{
    public override $$
}";

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync(filename, source, host);
            Assert.Equal(new[] { "Equals(object? obj)", "GetHashCode()", "ToString()" },
                         completions.Items.Select(c => c.Label));
            Assert.Equal(new[] { "Equals", "GetHashCode", "ToString" },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), a => Assert.Null(a));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.PlainText, c.InsertTextFormat));

            var afterInsert = await AfterInsertResponse(completions.Items[0], host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("bool Equals(object obj)\n    {\n        return base.Equals(obj);\n    }", change.NewText);
            Assert.Equal(3, change.StartLine);
            Assert.Equal(20, change.StartColumn);
            Assert.Equal(3, change.EndLine);
            Assert.Equal(26, change.EndColumn);
            Assert.Equal(5, afterInsert.Line);
            Assert.Equal(32, afterInsert.Column);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_ModifierInFront_Sync(string filename)
        {
            const string source = @"
class C
{
    public override $$
}";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Equal(new[] { "Equals(object? obj)", "GetHashCode()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "bool Equals(object obj)\n    {\n        return base.Equals(obj);$0\n    \\}",
                                 "int GetHashCode()\n    {\n        return base.GetHashCode();$0\n    \\}",
                                 "string ToString()\n    {\n        return base.ToString();$0\n    \\}"
                               },
                         completions.Items.Select(c => c.TextEdit.NewText));

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
        public async Task OverrideSignatures_PartiallyTypedMethod_Async(string filename)
        {
            const string source = @"
class C
{
    override Eq$$
}";

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync(filename, source, host);
            Assert.Equal(new[] { "Equals(object? obj)", "GetHashCode()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "Equals", "GetHashCode", "ToString" },
                         completions.Items.Select(c => c.TextEdit.NewText));
            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), a => Assert.Null(a));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.PlainText, c.InsertTextFormat));

            var afterInsert = await AfterInsertResponse(completions.Items[0], host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("public override bool Equals(object obj)\n    {\n        return base.Equals(obj);\n    }", change.NewText);
            Assert.Equal(3, change.StartLine);
            Assert.Equal(4, change.StartColumn);
            Assert.Equal(3, change.EndLine);
            Assert.Equal(19, change.EndColumn);
            Assert.Equal(5, afterInsert.Line);
            Assert.Equal(32, afterInsert.Column);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_PartiallyTypedMethod_Sync(string filename)
        {
            const string source = @"
class C
{
    override Eq$$
}";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Equal(new[] { "Equals(object? obj)", "GetHashCode()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "public override bool Equals(object obj)\n    {\n        return base.Equals(obj);$0\n    \\}",
                                 "public override int GetHashCode()\n    {\n        return base.GetHashCode();$0\n    \\}",
                                 "public override string ToString()\n    {\n        return base.ToString();$0\n    \\}"
                               },
                         completions.Items.Select(c => c.TextEdit.NewText));
            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), a => Assert.Null(a));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_ModifierAndReturnTypeInFront_Async(string filename)
        {
            const string source = @"
class C
{
    public override bool $$
}";

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync(filename, source, host);
            Assert.Equal(new[] { "Equals(object? obj)" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "Equals" },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), edit => Assert.Null(edit));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.PlainText, c.InsertTextFormat));

            var afterInsert = await AfterInsertResponse(completions.Items[0], host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("(object obj)\n    {\n        return base.Equals(obj);\n    }", change.NewText);
            Assert.Equal(3, change.StartLine);
            Assert.Equal(31, change.StartColumn);
            Assert.Equal(3, change.EndLine);
            Assert.Equal(31, change.EndColumn);
            Assert.Equal(5, afterInsert.Line);
            Assert.Equal(32, afterInsert.Column);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_ModifierAndReturnTypeInFront_Sync(string filename)
        {
            const string source = @"
class C
{
    public override bool $$
}";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Equal(new[] { "Equals(object? obj)" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "Equals(object obj)\n    {\n        return base.Equals(obj);$0\n    \\}" },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.Equal(new[] { "Equals" },
                         completions.Items.Select(c => c.FilterText));

            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), a => Assert.Null(a));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_TestTest_Async(string filename)
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

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync(filename, source, host);
            Assert.Equal(new[] { "Equals(object? obj)", "GetHashCode()", "Test()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "Equals", "GetHashCode", "Test", "ToString" },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), edit => Assert.Null(edit));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.PlainText, c.InsertTextFormat));

            var afterInsert = await AfterInsertResponse(completions.Items[0], host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("public override bool Equals(object obj)\n    {\n        return base.Equals(obj);\n    }", change.NewText);
            Assert.Equal(8, change.StartLine);
            Assert.Equal(4, change.StartColumn);
            Assert.Equal(8, change.EndLine);
            Assert.Equal(19, change.EndColumn);
            Assert.Equal(10, afterInsert.Line);
            Assert.Equal(32, afterInsert.Column);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_TestTest_Sync(string filename)
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

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Equal(new[] { "Equals(object? obj)", "GetHashCode()", "Test()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "public override bool Equals(object obj)\n    {\n        return base.Equals(obj);$0\n    \\}",
                                 "public override int GetHashCode()\n    {\n        return base.GetHashCode();$0\n    \\}",
                                 "protected override Test Test()\n    {\n        throw new System.NotImplementedException();$0\n    \\}",
                                 "public override string ToString()\n    {\n        return base.ToString();$0\n    \\}"
                               },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.Equal(new[] { "override Equals",
                                 "override GetHashCode",
                                 "override Test",
                                 "override ToString"
                               },
                         completions.Items.Select(c => c.FilterText));

            Assert.All(completions.Items,
                       c =>
                       {
                           Assert.Equal(8, c.TextEdit.StartLine);
                           Assert.Equal(4, c.TextEdit.StartColumn);
                           Assert.Equal(8, c.TextEdit.EndLine);
                           Assert.Equal(13, c.TextEdit.EndColumn);
                       });

            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
            Assert.All(completions.Items, c => Assert.Null(c.AdditionalTextEdits));
        }

        [Fact]
        public async Task OverrideCompletion_TypesNeedImport_Async()
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

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync("derived.cs", derivedText, host, additionalFiles: new[] { new TestFile("base.cs", baseText) });
            var item = completions.Items.Single(c => c.Label.StartsWith("GetAction"));
            Assert.Equal("GetAction(System.Action a)", item.Label);
            Assert.Equal("GetAction", item.TextEdit.NewText);

            Assert.Null(item.AdditionalTextEdits);

            var afterInsert = await AfterInsertResponse(item, host);
            Assert.Equal(2, afterInsert.Changes.Count);

            var firstChange = afterInsert.Changes[0];
            Assert.Equal(NormalizeNewlines("using System;\n\n"), firstChange.NewText);
            Assert.Equal(1, firstChange.StartLine);
            Assert.Equal(0, firstChange.StartColumn);
            Assert.Equal(1, firstChange.EndLine);
            Assert.Equal(0, firstChange.EndColumn);

            var secondChange = afterInsert.Changes[1];
            Assert.Equal(NormalizeNewlines("public override Action GetAction(Action a)\n    {\n        return base.GetAction(a);\n    }"), secondChange.NewText);
            Assert.Equal(3, secondChange.StartLine);
            Assert.Equal(4, secondChange.StartColumn);
            Assert.Equal(3, secondChange.EndLine);
            Assert.Equal(22, secondChange.EndColumn);

            Assert.Equal(7, afterInsert.Line);
            Assert.Equal(33, afterInsert.Column);
        }

        [Fact]
        public async Task OverrideCompletion_TypesNeedImport_Sync()
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

            var completions = await FindCompletionsAsync("derived.cs", derivedText, SharedOmniSharpTestHost, additionalFiles: new[] { new TestFile("base.cs", baseText) });
            var item = completions.Items.Single(c => c.Label.StartsWith("GetAction"));
            Assert.Equal("GetAction(System.Action a)", item.Label);

            Assert.Single(item.AdditionalTextEdits);
            Assert.Equal(NormalizeNewlines("using System;\n\n"), item.AdditionalTextEdits[0].NewText);
            Assert.Equal(1, item.AdditionalTextEdits[0].StartLine);
            Assert.Equal(0, item.AdditionalTextEdits[0].StartColumn);
            Assert.Equal(1, item.AdditionalTextEdits[0].EndLine);
            Assert.Equal(0, item.AdditionalTextEdits[0].EndColumn);
            Assert.Equal(NormalizeNewlines("public override Action GetAction(Action a)\n    {\n        return base.GetAction(a);$0\n    \\}"), item.TextEdit.NewText);
            Assert.Equal(3, item.TextEdit.StartLine);
            Assert.Equal(4, item.TextEdit.StartColumn);
            Assert.Equal(3, item.TextEdit.EndLine);
            Assert.Equal(13, item.TextEdit.EndColumn);
            Assert.Equal("override GetAction", item.FilterText);
        }

        [Fact]
        public async Task OverrideCompletion_FromNullableToNonNullableContext_Async()
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

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync("derived.cs", text, host);
            var item = completions.Items.Single(c => c.Label.StartsWith("M1"));
            Assert.Equal("M1(object? param)", item.Label);
            Assert.Equal("M1", item.TextEdit.NewText);

            Assert.Null(item.AdditionalTextEdits);

            var afterInsert = await AfterInsertResponse(completions.Items[0], host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("public override bool Equals(object obj)\n    {\n        return base.Equals(obj);\n    }", change.NewText);
            Assert.Equal(9, change.StartLine);
            Assert.Equal(4, change.StartColumn);
            Assert.Equal(9, change.EndLine);
            Assert.Equal(19, change.EndColumn);
            Assert.Equal(11, afterInsert.Line);
            Assert.Equal(32, afterInsert.Column);
        }

        [Fact]
        public async Task OverrideCompletion_FromNullableToNonNullableContext_Sync()
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

            var completions = await FindCompletionsAsync("derived.cs", text, SharedOmniSharpTestHost);
            var item = completions.Items.Single(c => c.Label.StartsWith("M1"));
            Assert.Equal("M1(object? param)", item.Label);

            Assert.Null(item.AdditionalTextEdits);
            Assert.Equal(9, item.TextEdit.StartLine);
            Assert.Equal(4, item.TextEdit.StartColumn);
            Assert.Equal(9, item.TextEdit.EndLine);
            Assert.Equal(13, item.TextEdit.EndColumn);
            Assert.Equal("public override object M1(object param)\n    {\n        return base.M1(param);$0\n    \\}", item.TextEdit.NewText);
            Assert.Equal("override M1", item.FilterText);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideCompletion_PropertyGetSet_Sync(string filename)
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

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            var item = completions.Items.Single(c => c.Label.StartsWith("Prop"));
            Assert.Equal("Prop", item.Label);

            Assert.Null(item.AdditionalTextEdits);
            Assert.Equal(8, item.TextEdit.StartLine);
            Assert.Equal(4, item.TextEdit.StartColumn);
            Assert.Equal(8, item.TextEdit.EndLine);
            Assert.Equal(13, item.TextEdit.EndColumn);
            Assert.Equal("public override string Prop { get => throw new NotImplementedException()$0; set => throw new NotImplementedException(); \\}", item.TextEdit.NewText);
            Assert.Equal("override Prop", item.FilterText);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideCompletion_PropertyGetSet_Async(string filename)
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

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync(filename, source, host);
            var item = completions.Items.Single(c => c.Label.StartsWith("Prop"));
            Assert.Equal("Prop", item.Label);
            Assert.Equal("Prop", item.TextEdit.NewText);

            Assert.Null(item.AdditionalTextEdits);

            var afterInsert = await AfterInsertResponse(item, host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("public override string Prop { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }", change.NewText);
            Assert.Equal(8, change.StartLine);
            Assert.Equal(4, change.StartColumn);
            Assert.Equal(8, change.EndLine);
            Assert.Equal(17, change.EndColumn);
            Assert.Equal(8, afterInsert.Line);
            Assert.Equal(76, afterInsert.Column);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideCompletion_PropertyGet_Sync(string filename)
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

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            var item = completions.Items.Single(c => c.Label.StartsWith("Prop"));
            Assert.Equal("Prop", item.Label);

            Assert.Null(item.AdditionalTextEdits);
            Assert.Equal(8, item.TextEdit.StartLine);
            Assert.Equal(4, item.TextEdit.StartColumn);
            Assert.Equal(8, item.TextEdit.EndLine);
            Assert.Equal(13, item.TextEdit.EndColumn);
            Assert.Equal("public override string Prop => throw new NotImplementedException()$0;", item.TextEdit.NewText);
            Assert.Equal(InsertTextFormat.Snippet, item.InsertTextFormat);
            Assert.Equal("override Prop", item.FilterText);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task PartialCompletion_Async(string filename)
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

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync(filename, source, host);
            Assert.Equal(new[] { "M1(string param)" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "M1" },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), edit => Assert.Null(edit));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.PlainText, c.InsertTextFormat));

            var afterInsert = await AfterInsertResponse(completions.Items[0], host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("void M1(string param)\n    {\n        throw new System.NotImplementedException();\n    }", change.NewText);
            Assert.Equal(7, change.StartLine);
            Assert.Equal(12, change.StartColumn);
            Assert.Equal(7, change.EndLine);
            Assert.Equal(14, change.EndColumn);
            Assert.Equal(9, afterInsert.Line);
            Assert.Equal(51, afterInsert.Column);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task PartialCompletion_Sync(string filename)
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

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Equal(new[] { "M1(string param)" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "void M1(string param)\n    {\n        throw new System.NotImplementedException();$0\n    \\}" },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), a => Assert.Null(a));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
        }

        [Fact]
        public async Task PartialCompletion_TypesNeedImport_Async()
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

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync("derived.cs", file2, host, additionalFiles: new[] { new TestFile("base.cs", file1) });
            var item = completions.Items.Single(c => c.Label.StartsWith("M"));

            Assert.Null(item.AdditionalTextEdits);
            Assert.Equal("M", item.TextEdit.NewText);

            var afterInsert = await AfterInsertResponse(item, host);
            Assert.Equal(2, afterInsert.Changes.Count);

            var firstChange = afterInsert.Changes[0];
            Assert.Equal(NormalizeNewlines("using System;\n\n"), firstChange.NewText);
            Assert.Equal(1, firstChange.StartLine);
            Assert.Equal(0, firstChange.StartColumn);
            Assert.Equal(1, firstChange.EndLine);
            Assert.Equal(0, firstChange.EndColumn);

            var secondChange = afterInsert.Changes[1];
            Assert.Equal(NormalizeNewlines("void M(Action a)\n    {\n        throw new NotImplementedException();\n    }"), secondChange.NewText);
            Assert.Equal(3, secondChange.StartLine);
            Assert.Equal(12, secondChange.StartColumn);
            Assert.Equal(3, secondChange.EndLine);
            Assert.Equal(13, secondChange.EndColumn);

            Assert.Equal(7, afterInsert.Line);
            Assert.Equal(44, afterInsert.Column);
        }

        [Fact]
        public async Task PartialCompletion_TypesNeedImport_Sync()
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

            var completions = await FindCompletionsAsync("derived.cs", file2, SharedOmniSharpTestHost, additionalFiles: new[] { new TestFile("base.cs", file1) });
            var item = completions.Items.Single(c => c.Label.StartsWith("M"));

            Assert.Single(item.AdditionalTextEdits);
            Assert.Equal(NormalizeNewlines("using System;\n\n"), item.AdditionalTextEdits[0].NewText);
            Assert.Equal(1, item.AdditionalTextEdits[0].StartLine);
            Assert.Equal(0, item.AdditionalTextEdits[0].StartColumn);
            Assert.Equal(1, item.AdditionalTextEdits[0].EndLine);
            Assert.Equal(0, item.AdditionalTextEdits[0].EndColumn);

            Assert.Equal("void M(Action a)\n    {\n        throw new NotImplementedException();$0\n    \\}", item.TextEdit.NewText);
            Assert.Equal("M", item.FilterText);
            Assert.Equal(3, item.TextEdit.StartLine);
            Assert.Equal(12, item.TextEdit.StartColumn);
            Assert.Equal(3, item.TextEdit.EndLine);
            Assert.Equal(12, item.TextEdit.EndColumn);
        }

        [Fact]
        public async Task PartialCompletion_FromNullableToNonNullableContext_Async()
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

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync("derived.cs", text, host);
            var item = completions.Items.Single(c => c.Label.StartsWith("M1"));
            Assert.Equal("M1(object param)", item.Label);
            Assert.Null(item.AdditionalTextEdits);
            Assert.Equal("M1", item.TextEdit.NewText);

            var afterInsert = await AfterInsertResponse(item, host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("void M1(object param)\n    {\n        throw new System.NotImplementedException();\n    }", change.NewText);
            Assert.Equal(9, change.StartLine);
            Assert.Equal(12, change.StartColumn);
            Assert.Equal(9, change.EndLine);
            Assert.Equal(14, change.EndColumn);
            Assert.Equal(11, afterInsert.Line);
            Assert.Equal(51, afterInsert.Column);
        }

        [Fact]
        public async Task PartialCompletion_FromNullableToNonNullableContext_Sync()
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

            var completions = await FindCompletionsAsync("derived.cs", text, SharedOmniSharpTestHost);
            var item = completions.Items.Single(c => c.Label.StartsWith("M1"));
            Assert.Equal("M1(object param)", item.Label);
            Assert.Null(item.AdditionalTextEdits);
            Assert.Equal("void M1(object param)\n    {\n        throw new System.NotImplementedException();$0\n    \\}", item.TextEdit.NewText);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_PartiallyTypedIdentifier_Async(string filename)
        {
            const string source = @"
class C
{
    override Ge$$
}";

            using var host = GetAsyncCompletionAndImportCompletionHost();
            var completions = await FindCompletionsAsync(filename, source, host);
            Assert.Equal(new[] { "Equals(object? obj)", "GetHashCode()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "Equals", "GetHashCode", "ToString" },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.All(completions.Items.Select(c => c.AdditionalTextEdits), edit => Assert.Null(edit));
            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.PlainText, c.InsertTextFormat));

            var afterInsert = await AfterInsertResponse(completions.Items[0], host);
            var change = afterInsert.Changes.Single();
            Assert.Equal("public override bool Equals(object obj)\n    {\n        return base.Equals(obj);\n    }", change.NewText);
            Assert.Equal(3, change.StartLine);
            Assert.Equal(4, change.StartColumn);
            Assert.Equal(3, change.EndLine);
            Assert.Equal(19, change.EndColumn);
            Assert.Equal(5, afterInsert.Line);
            Assert.Equal(32, afterInsert.Column);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task OverrideSignatures_PartiallyTypedIdentifier_Sync_Sync(string filename)
        {
            const string source = @"
class C
{
    override Ge$$
}";

            var completions = await FindCompletionsAsync(filename, source, SharedOmniSharpTestHost);
            Assert.Equal(new[] { "Equals(object? obj)", "GetHashCode()", "ToString()" },
                         completions.Items.Select(c => c.Label));

            Assert.Equal(new[] { "public override bool Equals(object obj)\n    {\n        return base.Equals(obj);$0\n    \\}",
                                 "public override int GetHashCode()\n    {\n        return base.GetHashCode();$0\n    \\}",
                                 "public override string ToString()\n    {\n        return base.ToString();$0\n    \\}"
                               },
                         completions.Items.Select(c => c.TextEdit.NewText));

            Assert.Equal(new[] { "override Equals",
                                 "override GetHashCode",
                                 "override ToString"
                               },
                         completions.Items.Select(c => c.FilterText));

            Assert.All(completions.Items, c => Assert.Null(c.AdditionalTextEdits));

            Assert.All(completions.Items.Select(c => c.TextEdit),
                       r =>
                       {
                           Assert.Equal(3, r.StartLine);
                           Assert.Equal(4, r.StartColumn);
                           Assert.Equal(3, r.EndLine);
                           Assert.Equal(15, r.EndColumn);
                       });

            Assert.All(completions.Items, c => Assert.False(c.Preselect));

            Assert.All(completions.Items, c => Assert.Equal(InsertTextFormat.Snippet, c.InsertTextFormat));
        }
    }
}
