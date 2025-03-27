using System;
using Microsoft.Extensions.Logging;
using OmniSharp.MSBuild.Discovery;
using OmniSharp.Roslyn;
using OmniSharp.Utilities;

namespace OmniSharp
{
    public class HostHelpers
    {
        public static int Start(Func<int> action)
        {
            try
            {
                return action();
            }
            catch (MSBuildNotFoundException mnfe)
            {
                Console.Error.WriteLine(mnfe.Message);
                return 0xbad;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
                return 0xbad;
            }
        }
    }
}
