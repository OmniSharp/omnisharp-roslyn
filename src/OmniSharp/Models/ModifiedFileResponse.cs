using System;

namespace OmniSharp.Models
{
    public class ModifiedFileResponse
    {
        public ModifiedFileResponse() { }

        public ModifiedFileResponse(string fileName, string buffer)
        {
            FileName = fileName;
            Buffer = buffer;
        }

        public string FileName { get; set; }
        public string Buffer { get; set; }

    }
}