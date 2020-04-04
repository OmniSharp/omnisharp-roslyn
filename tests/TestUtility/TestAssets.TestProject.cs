using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace TestUtility
{
    public partial class TestAssets
    {
        private class TestProject : ITestProject
        {
            private HashSet<string> _disposableFiles = new HashSet<string>();
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

            public string AddDisposableFile(string fileName, string contents = null)
            {
                var filePath = Path.Combine(Directory, fileName);
                File.WriteAllText(filePath, contents ?? string.Empty);
                _disposableFiles.Add(filePath);

                return filePath;
            }

            public virtual void Dispose()
            {
                if (_disposed)
                {
                    throw new InvalidOperationException($"{nameof(ITestProject)} for {this.Name} already disposed.");
                }

                if (this.ShadowCopied)
                {
                    RunWithRetry(() => System.IO.Directory.Delete(this.BaseDirectory, recursive: true));
                    if (System.IO.Directory.Exists(this.BaseDirectory))
                    {
                        throw new InvalidOperationException($"{nameof(ITestProject)} directory still exists: '{this.BaseDirectory}'");
                    }
                }
                else
                {
                    foreach (var filePath in _disposableFiles)
                    {
                        RunWithRetry(() => File.Delete(filePath));
                        if (System.IO.File.Exists(filePath))
                        {
                            throw new InvalidOperationException($"{nameof(ITestProject)} file still exists: '{filePath}'");
                        }
                    }
                }

                this._disposed = true;
                GC.SuppressFinalize(this);

                void RunWithRetry(Action action)
                {
                    var retries = 0;
                    while (retries <= 5)
                    {
                        try
                        {
                            action.Invoke();
                            break;
                        }
                        catch
                        {
                            Thread.Sleep(1000);
                            retries++;
                        }
                    }
                }
            }
        }
    }
}
