using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

public class Program
{
    public static int Main(string[] args)
    {
        var argsList = new List<string>(args);
        if (argsList.Contains("--debug"))
        {
            argsList.Remove("--debug");
            Console.WriteLine($"Attach debugger to process {Process.GetCurrentProcess().Id} to continue...");
            while (!Debugger.IsAttached)
            {
                Thread.Sleep(100);
            }
        }

        return OmniSharp.Program.Main(argsList.ToArray());
    }
}