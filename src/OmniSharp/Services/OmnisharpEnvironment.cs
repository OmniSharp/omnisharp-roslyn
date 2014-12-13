using System;
using Microsoft.Framework.Logging;

namespace OmniSharp.Services
{
    public class OmnisharpEnvironment : IOmnisharpEnvironment
    {
        public OmnisharpEnvironment(string path, TraceType traceType)
        {
            if (System.IO.Path.GetExtension(path).Equals(".sln", StringComparison.OrdinalIgnoreCase))
            {
                SolutionFilePath = path;
                Path = System.IO.Path.GetDirectoryName(path);
            }
            else
            {
                Path = path;
            }

            TraceType = traceType;
        }

        public TraceType TraceType { get; private set; }

        public string Path { get; private set; }

        public string SolutionFilePath { get; private set; }
    }
}