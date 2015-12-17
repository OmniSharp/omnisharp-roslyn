using System;
using OmniSharp.Host;

namespace OmniSharp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine($"OmniSharp: {string.Join(" ", args)}");
            HostRunner.Run(args);
        }
    }
}
