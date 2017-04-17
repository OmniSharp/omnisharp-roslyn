using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.DebugTestLaunch, typeof(DebugTestLaunchRequest), typeof(DebugTestLaunchResponse))]
    public class DebugTestLaunchRequest : Request
    {
        /// <summary>
        /// The PID of the process launched by the debugger.
        /// </summary>
        public int TargetProcessId { get; set; }
    }
}
