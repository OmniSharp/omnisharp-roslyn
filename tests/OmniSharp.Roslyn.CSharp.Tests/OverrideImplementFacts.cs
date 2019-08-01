using System.Threading.Tasks;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class OverrideImplementFacts : AbstractOverrideImplementTestFixture
    {
        public OverrideImplementFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        #region TestMethods

        /// <summary>
        /// Test the override of Equals method.
        /// </summary>
        /// <returns>Task</returns>
        [Fact]
        public async Task override_equals_method()
        {
            const string source = @"
public class Test
{
    override $$
}
";
            const string newText = @"public override bool Equals(object obj)
    {
        return base.Equals(obj);
    }";
            var expected = CreateExpect(source, newText);
            var actual = await OverrideImplementAsync("dummy.cs", source, "object.Equals(object)");
            Assert.Equal(expected.Changes, actual.Changes);
        }


        /// <summary>
        /// Test the override of virtual method.
        /// </summary>
        /// <returns>Task</returns>
        [Fact]
        public async Task override_virtual_method()
        {
            const string source = @"
public class Base
{
    protected virtual string VirtualMethod() { return null; }
}

public class Test : Base
{
    override $$
}
";
            const string newText = @"protected override string VirtualMethod()
    {
        return base.VirtualMethod();
    }";
            var expected = CreateExpect(source, newText);
            var actual = await OverrideImplementAsync("dummy.cs", source, "Base.VirtualMethod()");
            Assert.Equal(expected.Changes, actual.Changes);
        }


        /// <summary>
        /// Test the override of abstract method.
        /// </summary>
        /// <returns>Task</returns>
        [Fact]
        public async Task override_abstract_method()
        {
            const string source = @"
public abstract class Base
{
    protected abstract string AbstractMethod();
}

public class Test : Base
{
    override $$
}
";
            const string newText = @"protected override string AbstractMethod()
    {
        throw new System.NotImplementedException();
    }";
            var expected = CreateExpect(source, newText);
            var actual = await OverrideImplementAsync("dummy.cs", source, "Base.AbstractMethod()");
            Assert.Equal(expected.Changes, actual.Changes);
        }


        /// <summary>
        /// Test the add of using namespace.
        /// </summary>
        /// <returns>Task</returns>
        [Fact]
        public async Task add_using_namespace()
        {
            const string source = @"
namespace Namespace1 {
    using System.IO;
    using System.Collections.Generic;
    public class Base
    {
        protected virtual List<FileInfo> GetFileInfoList() { return null; }
    }
}

public class Test : Namespace1.Base
{
    override $$
}
";
            const string @using = @"using System.Collections.Generic;
using System.IO;

namespace Namespace1
";

            const string text = @"protected override List<FileInfo> GetFileInfoList()
    {
        return base.GetFileInfoList();
    }";
            var expected = CreateExpect(0, 0, 1, 21, @using);
            expected.Add(CreateChange(source, text));
            var actual = await OverrideImplementAsync("dummy.cs", source, "Namespace1.Base.GetFileInfoList()");
            Assert.Equal(expected.Changes, actual.Changes);
        }


        /// <summary>
        /// Test the override of virtual property.
        /// </summary>
        /// <returns>Task</returns>
        [Fact]
        public async Task override_virtual_property()
        {
            const string source = @"
public class Base
{
    protected virtual string VirtualProperty { get; set; }
}

public class Test : Base
{
    override $$
}
";
            const string newText = @"protected override string VirtualProperty
    {
        get { return base.VirtualProperty; }
        set { base.VirtualProperty = value; }
    }";
            var expected = CreateExpect(source, newText);
            var actual = await OverrideImplementAsync("dummy.cs", source, "Base.VirtualProperty");
            Assert.Equal(expected.Changes, actual.Changes);
        }


        /// <summary>
        /// Test the override of abstract property.
        /// </summary>
        /// <returns>Task</returns>
        [Fact]
        public async Task override_abstract_property()
        {
            const string source = @"
public class Base
{
    protected abstract string AbstractProperty { get; set; }
}

public class Test : Base
{
    override $$
}
";
            const string newText = @"protected override string AbstractProperty
    {
        get { throw new System.NotImplementedException(); }
        set { throw new System.NotImplementedException(); }
    }";
            var expected = CreateExpect(source, newText);
            var actual = await OverrideImplementAsync("dummy.cs", source, "Base.AbstractProperty");
            Assert.Equal(expected.Changes, actual.Changes);
        }

        #endregion //TestMethods
    }
}
