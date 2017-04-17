namespace OmniSharp.DotNetTest.TestFrameworks
{
    internal class NUnitTestFramework : TestFramework
    {
        public override string FeatureName { get; } = "NUnitTestMethod";
        public override string Name { get; } = "nunit";
        public override string MethodArgument { get; } = "--test";

        protected override bool IsTestAttributeName(string typeName)
        {
            return typeName == "NUnit.Framework.TestAttribute"
                || typeName == "NUnit.Framework.TestCaseAttribute"
                || typeName == "NUnit.Framework.TestCaseSourceAttribute";
        }
    }
}
