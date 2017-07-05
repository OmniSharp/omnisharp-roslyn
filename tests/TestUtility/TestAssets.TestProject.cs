using System;
using System.Threading;

namespace TestUtility
{
    public partial class TestAssets
    {
        private class TestProject : ITestProject
        {
            private bool _disposed;

            public string Name { get; }
            public string BaseDirectory { get; }
            public string Directory { get; }
            public bool ShadowCopied { get; }

            public TestProject(string name, string baseDirectory, string directory, bool shadowCopied)
            {
                this.Name = name;
                this.BaseDirectory = baseDirectory;
                this.Directory = directory;
                this.ShadowCopied = shadowCopied;
            }

            ~TestProject()
            {
                throw new InvalidOperationException($"{nameof(ITestProject)}.{nameof(Dispose)}() not called for {this.Name}");
            }

            public virtual void Dispose()
            {
                if (_disposed)
                {
                    throw new InvalidOperationException($"{nameof(ITestProject)} for {this.Name} already disposed.");
                }

                if (this.ShadowCopied)
                {
                    var retries = 0;
                    while (retries <= 5)
                    {
                        try
                        {
                            System.IO.Directory.Delete(this.BaseDirectory, recursive: true);
                            break;
                        }
                        catch
                        {
                            Thread.Sleep(1000);
                            retries++;
                        }
                    }

                    if (System.IO.Directory.Exists(this.BaseDirectory))
                    {
                        throw new InvalidOperationException($"{nameof(ITestProject)} directory still exists: '{this.BaseDirectory}'");
                    }
                }

                this._disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
