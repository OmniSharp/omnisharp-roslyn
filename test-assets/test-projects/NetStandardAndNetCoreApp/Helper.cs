using System;
using Newtonsoft.Json.Linq;

namespace TestLibrary
{
    public static class Helper
    {
        public static void SayHi()
        {
            Console.WriteLine("Hello there!");
            Console.WriteLine(typeof(JObject));
        }

#if NETCOREAPP
        static void Main() => SayHi();
#endif
    }
}