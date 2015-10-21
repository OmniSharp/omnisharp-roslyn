using System.Threading.Tasks;

namespace OmniSharp.Stdio.Services
{
    public interface ISharedTextWriter
    {
        void WriteLine(object value);

        Task WriteLineAsync(object value);
    }
}
