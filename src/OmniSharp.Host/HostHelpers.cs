using System;
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
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
                return 0xbad;
            }
        }
    }
}
