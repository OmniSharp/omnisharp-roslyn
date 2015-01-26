using System;
using OmniSharp.Models;

namespace OmniSharp
{
    public static class WorkspaceExtensions
    {
        public static void EnsureBufferUpdated(this OmnisharpWorkspace workspace, Request request)
        {
            workspace.EnsureBufferUpdated(request.Buffer, request.FileName);
        }
    }
}