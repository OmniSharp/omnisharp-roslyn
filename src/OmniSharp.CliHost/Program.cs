using System;
using OmniSharp.Host;

namespace OmniSharp.CliHost
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine($"OmniSharp-CLI: {string.Join(" ", args)}");
            HostRunner.Run<CliOmnisharpStartup>(args);
        }
    }
}
