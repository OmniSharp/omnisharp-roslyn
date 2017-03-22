using System;

namespace TestUtility
{
    public interface ITestProject : IDisposable
    {
        string Name { get; }
        string BaseDirectory { get; }
        string Directory { get; }
        bool ShadowCopied { get; }
    }
}
