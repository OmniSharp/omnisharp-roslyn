using System.Collections.Generic;
using TestUtility;
using Xunit.Abstractions;

namespace OmniSharp.DotNetTest.Tests
{
    public abstract class AbstractTestFixture : TestUtility.AbstractTestFixture
    {
        protected static readonly Dictionary<string, string> ConfigurationData = new Dictionary<string, string>
        {
            ["DotNet:Enabled"] = "true"
        };

        protected const string LegacyXunitTestProject = nameof(LegacyXunitTestProject);
        protected const string LegacyNUnitTestProject = nameof(LegacyNUnitTestProject);
        protected const string LegacyMSTestProject = nameof(LegacyMSTestProject);
        protected const string XunitTestProject = nameof(XunitTestProject);
        protected const string NUnitTestProject = nameof(NUnitTestProject);
        protected const string MSTestProject = nameof(MSTestProject);

        protected AbstractTestFixture(ITestOutputHelper output)
            : base(output)
        {
        }

        public abstract DotNetCliVersion DotNetCliVersion { get; }
    }
}
