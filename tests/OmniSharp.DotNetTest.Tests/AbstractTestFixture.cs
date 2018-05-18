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

        protected const string LegacyXunitTestProject = "LegacyXunitTestProject";
        protected const string LegacyNunitTestProject = "LegacyNUnitTestProject";
        protected const string LegacyMSTestProject = "LegacyMSTestProject";
        protected const string XunitTestProject = "XunitTestProject";
        protected const string NUnitTestProject = "NUnitTestProject";
        protected const string MSTestProject = "MSTestProject";

        protected AbstractTestFixture(ITestOutputHelper output)
            : base(output)
        {
        }

        public abstract DotNetCliVersion DotNetCliVersion { get; }
    }
}
