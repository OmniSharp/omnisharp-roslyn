using System;

namespace OmniSharp.Stdio.Services
{
    public class SharedConsoleWriter : SharedTextWriter
    {
        public SharedConsoleWriter() : base(Console.Out) { }
    }
}
