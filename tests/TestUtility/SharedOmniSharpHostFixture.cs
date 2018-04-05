using System;

namespace TestUtility
{
    public class SharedOmniSharpHostFixture : IDisposable
    {
        public SharedOmniSharpHostFixture()
        {
        }

        public void Dispose()
        {
            OmniSharpTestHost?.Dispose();
        }

        public OmniSharpTestHost OmniSharpTestHost { get; set; }

    }
}
