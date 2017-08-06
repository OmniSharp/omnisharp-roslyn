using System;
using System.Collections.Generic;
using System.IO;

namespace OmniSharp.MSBuild.SolutionParsing
{
    internal class Scanner : IDisposable
    {
        private readonly StringReader _reader;
        private int _currentLineNumber;

        public Scanner(string text)
        {
            _reader = new StringReader(text);
        }

        public void Dispose()
        {
            _reader.Dispose();
        }

        public string NextLine()
        {
            var line = _reader.ReadLine();

            _currentLineNumber++;

            if (line != null)
            {
                line = line.Trim();
            }

            return line;
        }
    }
}
