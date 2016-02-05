using System;

namespace OmniSharp
{
    public static class PlatformHelper
    {
        private static Lazy<bool> _isMono = new Lazy<bool>(() => Type.GetType("Mono.Runtime") != null);
        private static Lazy<string> _OSString = new Lazy<String>(() => 
            System.IO.Path.DirectorySeparatorChar == '\\' ? "win" : "linux");

        public static bool IsMono
        {
            get
            {
                return _isMono.Value;
            }
        }
        
        public static string OSString
        {
            get
            {
                return _OSString.Value;
            }
        }
    }
}
