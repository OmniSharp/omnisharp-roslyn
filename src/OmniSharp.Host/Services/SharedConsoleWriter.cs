using System;

namespace OmniSharp.Services
{
    public class SharedConsoleWriter : SharedTextWriter
    {
        public SharedConsoleWriter() : base(Console.Out) { }
    }
}
