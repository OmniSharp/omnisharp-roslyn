using System;
using System.IO;
using System.Threading.Tasks;

namespace OmniSharp.Stdio.Services
{
    public interface ISharedTextWriter
    {
        void Use(Action<TextWriter> callback);
        Task Use(Func<TextWriter, Task> callback);
    }
}
