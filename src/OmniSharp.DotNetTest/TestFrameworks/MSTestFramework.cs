namespace OmniSharp.DotNetTest.TestFrameworks
{
    internal class MSTestFramework : TestFramework
    {
        public override string FeatureName { get; } = "MSTestMethod";
        public override string Name { get; } = "mstest";
        public override string MethodArgument { get; } = "--test";

        protected override bool IsTestAttributeName(string typeName)
        {
            return typeName == "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute";
        }
    }
}
