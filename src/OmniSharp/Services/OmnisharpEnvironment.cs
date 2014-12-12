using System;

namespace OmniSharp.Services
{
    public class OmnisharpEnvironment : IOmnisharpEnvironment
    {
        public OmnisharpEnvironment(string path)
        {
            SolutionRoot = path;
        }

        public string SolutionRoot { get; private set; }
    }
}