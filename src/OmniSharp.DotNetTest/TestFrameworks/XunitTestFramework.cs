using System;

namespace OmniSharp.DotNetTest.TestFrameworks
{
    internal class XunitTestFramework : TestFramework
    {
        public override string FeatureName { get; } = "XunitTestMethod";
        public override string Name { get; } = "xunit";
        public override string MethodArgument { get; } = "-method";

        protected override bool IsTestAttributeName(string typeName)
        {
            return typeName == "Xunit.FactAttribute";
        }
    }
}
