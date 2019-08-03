using System;

namespace TestUtility
{
    public class SharedOmniSharpHostFixture : IDisposable
    {
        public SharedOmniSharpHostFixture()
        {
        }

        static SharedOmniSharpHostFixture()
        {
            TestHelpers.SetDefaultCulture();
        }

        public void Dispose()
        {
            OmniSharpTestHost?.Dispose();
        }

        public OmniSharpTestHost OmniSharpTestHost { get; set; }

    }
}
