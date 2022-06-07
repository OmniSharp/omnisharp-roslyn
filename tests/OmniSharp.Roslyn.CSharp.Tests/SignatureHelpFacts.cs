using System;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models.SignatureHelp;
using OmniSharp.Roslyn.CSharp.Services.Signatures;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class SignatureHelpFacts : AbstractSingleRequestHandlerTestFixture<SignatureHelpService>
    {
        public SignatureHelpFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.SignatureHelp;

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task NoInvocationNoHelp1(string filename)
        {
            const string source =
@"class Program
{
    public static void Ma$$in(){
        System.Guid.NoSuchMethod();
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Null(actual);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task NoInvocationNoHelp2(string filename)
        {
            const string source =
@"class Program
{
    public static void Main(){
        System.Gu$$id.NoSuchMethod();
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Null(actual);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task NoInvocationNoHelp3(string filename)
        {
            const string source =
@"class Program
{
    public static void Main(){
        System.Guid.NoSuchMethod()$$;
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Null(actual);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task NoTypeNoHelp(string filename)
        {
            const string source =
@"class Program
{
    public static void Main(){
        System.Guid.Foo$$Bar();
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Null(actual);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task NoMethodNoHelp(string filename)
        {
            const string source =
@"class Program
{
    public static void Main(){
        System.Gu$$id;
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Null(actual);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task SimpleSignatureHelp(string filename)
        {
            const string source =
@"class Program
{
    public static void Main(){
        System.Guid.NewGuid($$);
    }
}";

            var actual = await GetSignatureHelp(filename, source);
            Assert.Single(actual.Signatures);
            Assert.Equal(0, actual.ActiveParameter);
            Assert.Equal(0, actual.ActiveSignature);
            Assert.Equal("NewGuid", actual.Signatures.ElementAt(0).Name);
            Assert.Empty(actual.Signatures.ElementAt(0).Parameters);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task TestForParameterLabels(string filename)
        {
            const string source =
@"class Program
{
    public static void Main(){
        Foo($$);
    }
    public static void Foo(bool b, int n = 1234)
    {
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Single(actual.Signatures);
            Assert.Equal(0, actual.ActiveParameter);
            Assert.Equal(0, actual.ActiveSignature);

            var signature = actual.Signatures.ElementAt(0);
            Assert.Equal(2, signature.Parameters.Count());
            Assert.Equal("b", signature.Parameters.ElementAt(0).Name);
            Assert.Equal("bool b", signature.Parameters.ElementAt(0).Label);
            Assert.Equal("n", signature.Parameters.ElementAt(1).Name);
            Assert.Equal("int n = 1234", signature.Parameters.ElementAt(1).Label);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ActiveParameterIsBasedOnComma1(string filename)
        {
            // 1st position, a
            const string source =
@"class Program
{
    public static void Main(){
        new Program().Foo(1$$2,
    }
    /// foo1
    private int Foo(int one, int two, int three)
    {
        return 3;
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Equal(0, actual.ActiveParameter);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task AttributeCtorSingleParam(string filename)
        {
            const string source =
@"using System;
[MyTest($$)]
public class TestClass
{
    public static void Main()
    {
    }
}
public class MyTestAttribute : Attribute
{
    public MyTestAttribute(int value)
    {
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Equal(0, actual.ActiveParameter);

            var signature = actual.Signatures.ElementAt(0);
            Assert.Single(signature.Parameters);
            Assert.Equal("value", signature.Parameters.ElementAt(0).Name);
            Assert.Equal("int value", signature.Parameters.ElementAt(0).Label);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task AttributeCtorTestParameterLabels(string filename)
        {
            const string source =
@"using System;
[MyTest($$)]
public class TestClass
{
    public static void Main()
    {
    }
}
public class MyTestAttribute : Attribute
{
    public MyTestAttribute(int value1,double value2)
    {
    }
}
";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Equal(0, actual.ActiveParameter);

            var signature = actual.Signatures.ElementAt(0);
            Assert.Equal(2, signature.Parameters.Count());
            Assert.Equal("value1", signature.Parameters.ElementAt(0).Name);
            Assert.Equal("int value1", signature.Parameters.ElementAt(0).Label);
            Assert.Equal("value2", signature.Parameters.ElementAt(1).Name);
            Assert.Equal("double value2", signature.Parameters.ElementAt(1).Label);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task AttributeCtorActiveParamBasedOnComma(string filename)
        {
            const string source =
@"using System;
[MyTest(2,$$)]
public class TestClass
{
    public static void Main()
    {
    }
}
public class MyTestAttribute : Attribute
{
    public MyTestAttribute(int value1,double value2)
    {
    }
}
";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Equal(1, actual.ActiveParameter);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task AttributeCtorNoParam(string filename)
        {
            const string source =
@"using System;
[MyTest($$)]
public class TestClass
{
    public static void Main()
    {
    }
}
public class MyTestAttribute : Attribute
{
    public MyTestAttribute()
    {
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Single(actual.Signatures);
            Assert.Equal(0, actual.ActiveParameter);
            Assert.Equal(0, actual.ActiveSignature);
            Assert.Equal("MyTestAttribute", actual.Signatures.ElementAt(0).Name);
            Assert.Empty(actual.Signatures.ElementAt(0).Parameters);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ActiveParameterIsBasedOnComma2(string filename)
        {
            // 1st position, b
            const string source =
@"class Program
{
    public static void Main(){
        new Program().Foo(12 $$)
    }
    /// foo1
    private int Foo(int one, int two, int three)
    {
        return 3;
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Equal(0, actual.ActiveParameter);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ActiveParameterIsBasedOnComma3(string filename)
        {
            // 2nd position, a
            const string source =
@"class Program
{
    public static void Main(){
        new Program().Foo(12, $$
    }
    /// foo1
    private int Foo(int one, int two, int three)
    {
        return 3;
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Equal(1, actual.ActiveParameter);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ActiveParameterIsBasedOnComma4(string filename)
        {
            // 2nd position, b
            const string source =
@"class Program
{
    public static void Main(){
        new Program().Foo(12, 1$$
    }
    /// foo1
    private int Foo(int one, int two, int three)
    {
        return 3;
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Equal(1, actual.ActiveParameter);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ActiveParameterIsBasedOnComma5(string filename)
        {
            // 3rd position, a
            const string source =
@"class Program
{
    public static void Main(){
        new Program().Foo(12, 1, $$
    }
    /// foo1
    private int Foo(int one, int two, int three)
    {
        return 3;
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Equal(2, actual.ActiveParameter);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ActiveSignatureIsBasedOnTypes1(string filename)
        {
            const string source =
@"class Program
{
    public static void Main(){
        new Program().Foo(12, $$
    }
    /// foo1
    private int Foo()
    {
        return 3;
    }
    /// foo2
    private int Foo(int m, int n)
    {
        return m * Foo() * n;
    }
    /// foo3
    private int Foo(string m, int n)
    {
        return Foo(m.length, n);
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Equal(3, actual.Signatures.Count());
            Assert.Contains("foo2", actual.Signatures.ElementAt(actual.ActiveSignature).Documentation);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ActiveSignatureIsBasedOnTypes2(string filename)
        {
            const string source =
@"class Program
{
    public static void Main(){
        new Program().Foo(""d"", $$
    }
    /// foo1
    private int Foo()
    {
        return 3;
    }
    /// foo2
    private int Foo(int m, int n)
    {
        return m * Foo() * n;
    }
    /// foo3
    private int Foo(string m, int n)
    {
        return Foo(m.length, n);
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Equal(3, actual.Signatures.Count());
            Assert.Contains("foo3", actual.Signatures.ElementAt(actual.ActiveSignature).Documentation);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ActiveSignatureIsBasedOnTypes3(string filename)
        {
            const string source =
@"class Program
{
    public static void Main(){
        new Program().Foo($$)
    }
    /// foo1
    private int Foo()
    {
        return 3;
    }
    /// foo2
    private int Foo(int m, int n)
    {
        return m * Foo() * n;
    }
    /// foo3
    private int Foo(string m, int n)
    {
        return Foo(m.length, n);
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Equal(3, actual.Signatures.Count());
            Assert.Contains("foo1", actual.Signatures.ElementAt(actual.ActiveSignature).Documentation);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task TestForConstructorHelp(string filename)
        {
            const string source =
@"class Program
{
    public static void Main()
    {
        new Program($$)
    }
    public Program()
    {
    }
    public Program(bool b)
    {
    }
    public Program(Program p)
    {
    }
}";

            var actual = await GetSignatureHelp(filename, source);
            Assert.Equal(3, actual.Signatures.Count());
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task TestForImplicitCreationConstructorHelp(string filename)
        {
            const string source =
@"class Program
{
    public static void Main()
    {
        Program program = new($$)
    }
    public Program()
    {
    }
    public Program(bool b)
    {
    }
    public Program(Program p)
    {
    }
}";

            var actual = await GetSignatureHelp(filename, source);
            Assert.Equal(3, actual.Signatures.Count());
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task TestForImplicitCreationCtorWithOverloads(string filename)
        {
            const string source =
@"class Program
{
    public static void Main()
    {
        Program program = new(true, 12$$3)
    }
    public Program()
    {
    }
    /// ctor2
    public Program(bool b, int n)
    {
    }
    public Program(Program p, int n)
    {
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Equal(3, actual.Signatures.Count());
            Assert.Equal(1, actual.ActiveParameter);
            Assert.Contains("ctor2", actual.Signatures.ElementAt(actual.ActiveSignature).Documentation);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task TestForCtorWithOverloads(string filename)
        {
            const string source =
@"class Program
{
    public static void Main()
    {
        new Program(true, 12$$3)
    }
    public Program()
    {
    }
    /// ctor2
    public Program(bool b, int n)
    {
    }
    public Program(Program p, int n)
    {
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Equal(3, actual.Signatures.Count());
            Assert.Equal(1, actual.ActiveParameter);
            Assert.Contains("ctor2", actual.Signatures.ElementAt(actual.ActiveSignature).Documentation);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task TestForInheritedMethods(string filename)
        {
            const string source =
@"public class MyBase
{
    public void MyMethod(int a) { }
    public void MyMethod(int a, int b) { }
}

public class Class1 : MyBase
{
    public void MyMethod(int a, int b, int c) { }
    public void MyMethod(int a, int b, int c, int d) { }
}

public class Class2
{
    public void foo()
    {
        Class1 c1 = new Class1();
        c1.MyMethod($$);
    }

 }";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Equal(4, actual.Signatures.Count());
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task InheritedInaccesibleMethods(string filename)
        {
            const string source =
@"public class MyBase
{
    private void MyMethod(int a) { }
}

public class Class1 : MyBase
{
    public void MyMethod(int a, int b, int c) { }
    protected void MyMethod(int a, int b, int c, int d) { }
}

public class Class2
{
    public void foo()
    {
        Class1 c1 = new Class1();
        c1.MyMethod($$);
    }

 }";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Single(actual.Signatures);

            var signature = actual.Signatures.ElementAt(0);
            Assert.Equal(3, signature.Parameters.Count());
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task InheritedProtectedMethod(string filename)
        {
            const string source =
@"class A
{
    protected void M1() { }
}

class B : A
{
    void M1(int a)
    {
       M1($$)
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Equal(2, actual.Signatures.Count());
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task InheritedProtectedMethodWithThis(string filename)
        {
            const string source =
@"class A
{
    protected void M1() { }
}

class B : A
{
    void M1(int a)
    {
        this.M1($$)
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Equal(2, actual.Signatures.Count());
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task InheritedProtectedMethodWithBase(string filename)
        {
            const string source =
@"class A
{
    protected void M1() { }
}

class B : A
{
    void M1(int a)
    {
        base.M1($$);
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Single(actual.Signatures);
            Assert.Empty(actual.Signatures.ElementAt(0).Parameters);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task StaticContextMethod1(string filename)
        {
            const string source =
@"class A
{
    protected static void M1(int a) { }
    public void M1(double b) { }
}

class B : A
{
    static void M1()
    {
        A.M1($$);
    }
    public void M1(string c) { }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Single(actual.Signatures);

            var signature = actual.Signatures.ElementAt(0);
            Assert.Single(signature.Parameters);
            Assert.Equal("int a", signature.Parameters.ElementAt(0).Label);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task StaticContextMethod2(string filename)
        {
            const string source =
@"class A
{
    protected static void M1(int a) { }
    public void M1(int a,int b) { }
}

class B : A
{
    static void M1(int a,int b,int c)
    {
        B.M1($$)
    }
    public void M1(int a,int b,int c,int d) { }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Equal(2, actual.Signatures.Count());
            var signatures = actual.Signatures.OrderBy(sig => sig.Parameters.Count());
            Assert.Single(signatures.ElementAt(0).Parameters);
            Assert.Equal(3, signatures.ElementAt(1).Parameters.Count());
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task InstanceContextMethod(string filename)
        {
            const string source =
@"class A
{
    protected static void M1(int a) { }
    public void M1(int a, int b) { }
}

class B : A
{
    void M1(int a,int b,int c)
    {
        M1($$)
    }
    static void M1(int a,int b,int c,int d) { }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Equal(4, actual.Signatures.Count());
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task LiteralContextMethod(string filename)
        {
            const string source =
@"class A
{
    static void M()
    {
        string three = 3.ToString($$);
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Contains(actual.Signatures, signature => signature.Name == "ToString" && signature.Parameters.Count() == 0);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task TypeOfContextMethod(string filename)
        {
            const string source =
@"class A
{
    static void M()
    {
        var members = typeof(A).GetMembers($$);
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Contains(actual.Signatures, signature => signature.Name == "GetMembers" && signature.Parameters.Count() == 0);
        }

        [Fact]
        public async Task OverloadedExtensionMethods1()
        {
            const string source =
@"public static class ExtensionMethods
{
    public static void MyMethod(this string value, int number)
    {
    }
}

class Program
{
    public static void MyMethod(string a, int b)
    {
    }
    public static void Main()
    {
        string value = ""Hello"";
        value.MyMethod($$);
    }
}";
            var actual = await GetSignatureHelp("dummy.cs", source);
            Assert.Single(actual.Signatures);

            var signature = actual.Signatures.ElementAt(0);
            Assert.Equal("void string.MyMethod(int number)", signature.Label);
            Assert.Single(signature.Parameters);
            Assert.Equal("number", signature.Parameters.ElementAt(0).Name);
        }

        [Fact]
        public async Task OverloadedExtensionMethods1_CsxScript()
        {
            const string source =
@"public static void MyMethod(this string value, int number)
{
}

public static void MyMethod(string a, int b)
{
}

string value = ""Hello"";
value.MyMethod($$);
";
            var actual = await GetSignatureHelp("dummy.csx", source);
            Assert.Single(actual.Signatures);

            var signature = actual.Signatures.ElementAt(0);
            Assert.Equal("void string.MyMethod(int number)", signature.Label);
            Assert.Single(signature.Parameters);
            Assert.Equal("number", signature.Parameters.ElementAt(0).Name);
        }

        [Fact]
        public async Task OverloadedExtensionMethods2()
        {
            const string source =
@"public static class ExtensionMethods
{
    public static void MyMethod(this string value, int number)
    {
    }
}

class Program
{
    public static void MyMethod(string a, int b)
    {
    }
    public static void Main()
    {
        string value = ""Hello"";
        MyMethod($$);
    }
}";
            var actual = await GetSignatureHelp("dummy.cs", source);
            Assert.Single(actual.Signatures);

            var signature = actual.Signatures.ElementAt(0);
            Assert.Equal("void Program.MyMethod(string a, int b)", signature.Label);
        }

        [Fact]
        public async Task OverloadedExtensionMethods2_CsxScript()
        {
            const string source =
@"public static void MyMethod(this string value, int number)
{
}

class Program
{
    public static void MyMethod(string a, int b)
    {
    }
    public static void Main()
    {
        string value = ""Hello"";
        MyMethod($$);
    }
}";
            var actual = await GetSignatureHelp("dummy.csx", source);
            Assert.Single(actual.Signatures);

            var signature = actual.Signatures.ElementAt(0);
            Assert.Equal("void Program.MyMethod(string a, int b)", signature.Label);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task GivesHelpForLocalFunctionsInStaticContext(string filename)
        {
            const string source =
@"class Program
{
    public static void Main()
    {
        var flag = LocalFunction($$);
        bool LocalFunction(int i)
        {
            return i > 0;
        }
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Single(actual.Signatures);

            var signature = actual.Signatures.ElementAt(0);
            Assert.Single(signature.Parameters);
            Assert.Equal("i", signature.Parameters.ElementAt(0).Name);
            Assert.Equal("int i", signature.Parameters.ElementAt(0).Label);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task GivesHelpForLocalFunctionsInNonStaticContext(string filename)
        {
            const string source =
@"class Program
{
    public void Main()
    {
        var flag = LocalFunction($$);
        bool LocalFunction(int i)
        {
            return i > 0;
        }
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Single(actual.Signatures);

            var signature = actual.Signatures.ElementAt(0);
            Assert.Single(signature.Parameters);
            Assert.Equal("i", signature.Parameters.ElementAt(0).Name);
            Assert.Equal("int i", signature.Parameters.ElementAt(0).Label);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ReturnsDocumentationForParameters(string filename)
        {
            const string source =
@"class Program
{
    public static void Main()
    {
        var flag = Compare($$);
    }
    ///<summary>Checks if object is tagged with the tag.</summary>
    /// <param name=""gameObject"">The game object.</param>
    /// <param name=""tagName"">Name of the tag.</param>
    /// <returns>Returns <c> true</c> if object is tagged with tag.</returns>
    public static bool Compare(GameObject gameObject, string tagName)
    {
        return true;
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Single(actual.Signatures);

            var signature = actual.Signatures.ElementAt(0);
            Assert.Equal(2, signature.Parameters.Count());
            Assert.Equal("gameObject", signature.Parameters.ElementAt(0).Name);
            Assert.Equal("The game object.", signature.Parameters.ElementAt(0).Documentation);
            Assert.Equal("tagName", signature.Parameters.ElementAt(1).Name);
            Assert.Equal("Name of the tag.", signature.Parameters.ElementAt(1).Documentation);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ReturnsDocumentationForParametersNestedTags(string filename)
        {
            const string source =
@"class Program
{
    public static void Main()
    {
        var flag = Compare($$);
    }
    ///<summary>Checks if object is tagged with the tag.</summary>
    /// <param name=""gameObject"">The game object.</param>
    /// <param name=""tagName"">Name of the tag.It has the default value as <c>null</c></param>
    /// <returns>Returns <c> true</c> if object is tagged with tag.</returns>
    public static bool Compare(GameObject gameObject, string tagName = null)
    {
        return true;
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Single(actual.Signatures);

            var signature = actual.Signatures.ElementAt(0);
            Assert.Equal(2, signature.Parameters.Count());
            Assert.Equal("Name of the tag.It has the default value as null", signature.Parameters.ElementAt(1).Documentation);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ReturnsStructuredDocumentation(string filename)
        {
            const string source =
@"class Program
{
    public static void Main()
    {
        var flag = Compare($$);
    }
    ///<summary>Checks if object is tagged with the tag.</summary>
    /// <param name=""gameObject"">The game object.</param>
    /// <param name=""tagName"">Name of the tag.</param>
    /// <returns>Returns <c>true</c> if object is tagged with tag.</returns>
    public static bool Compare(GameObject gameObject, string tagName)
    {
        return true;
    }
}";
            var actual = await GetSignatureHelp(filename, source);
            Assert.Single(actual.Signatures);

            var signature = actual.Signatures.ElementAt(0);
            Assert.Equal("Checks if object is tagged with the tag.", signature.StructuredDocumentation.SummaryText);
            Assert.Equal("Returns true if object is tagged with tag.", signature.StructuredDocumentation.ReturnsText);
        }

        [Fact]
        public async Task SkipReceiverOfExtensionMethods()
        {
            const string source =
@"public class Program1
{
    public Program1() { }
}

public static class ExtensionClass{
    public static bool B(this Program1 p, int n)
    {
        return p.Foo() > n;
    }
}

public class ProgramClass
{
    public static void Main()
    {
        new Program1().B($$);
    }
}";
            var actual = await GetSignatureHelp("dummy.cs", source);
            Assert.Single(actual.Signatures);

            var signature = actual.Signatures.ElementAt(0);
            Assert.Single(signature.Parameters);
            Assert.Equal("n", signature.Parameters.ElementAt(0).Name);
            Assert.Equal("int n", signature.Parameters.ElementAt(0).Label);
        }

        [Fact]
        public async Task BaseObjectCreationWithoutArguments()
        {
            const string source =
@"public static class Foo {
    public static string GetClientCredentialsToken() {
        AccessTokenRequest request = new AccessTokenRequest {
            client$$Id = ""foo"",
        }
    }
}
";

            var actual = await GetSignatureHelp("dummy.cs", source);

            Assert.Null(actual);
        }

        private async Task<SignatureHelpResponse> GetSignatureHelp(string filename, string source)
        {
            var testFile = new TestFile(filename, source);
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);

            var point = testFile.Content.GetPointFromPosition();
            var request = new SignatureHelpRequest()
            {
                FileName = testFile.FileName,
                Line = point.Line,
                Column = point.Offset,
                Buffer = testFile.Content.Code
            };

            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);
            return await requestHandler.Handle(request);
        }
    }
}
