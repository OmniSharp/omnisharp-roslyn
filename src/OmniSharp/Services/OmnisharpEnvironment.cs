using System;
using Microsoft.Framework.Logging;

namespace OmniSharp.Services
{
    public class OmnisharpEnvironment : IOmnisharpEnvironment
    {
        public OmnisharpEnvironment(string path, TraceType traceType)
        {
            SolutionRoot = path;
            TraceType = traceType;
        }

        public TraceType TraceType { get; private set; }

        public string SolutionRoot { get; private set; }
    }
}