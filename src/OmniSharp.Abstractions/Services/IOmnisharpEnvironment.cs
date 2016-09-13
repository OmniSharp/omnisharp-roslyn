using System.Collections.Generic;
﻿using Microsoft.Extensions.Logging;

namespace OmniSharp.Services
{
    public interface IOmnisharpEnvironment
    {
        LogLevel TraceType { get; }
        int Port { get; }
        int HostPID { get; }
        string Path { get; }
        string SolutionFilePath { get; }
        TransportType TransportType { get; }
        string[] Plugins { get; }
    }
}
