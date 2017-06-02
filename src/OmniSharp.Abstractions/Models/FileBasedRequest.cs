using System.IO;

namespace OmniSharp.Models
{
    public class FileBasedRequest : IRequest
    {
        private string _fileName;

        /// <summary>
        /// The name of the file this request is based on.
        /// </summary>
        public string FileName
        {
            get => _fileName?.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            set => _fileName = value;
        }
    }
}
