using System;

namespace OmniSharp.Services
{
    public interface IOmnisharpEnvironment
    {
        string SolutionRoot { get; }
    }
}