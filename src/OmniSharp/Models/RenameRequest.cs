using System;

namespace OmniSharp.Models
{
    public class RenameRequest : Request
    {
        public string RenameTo { get; set; }
    }
}