extern alias abc;
using System;

namespace ExternAlias.App
{
    class Program
    {
        static void Main(string[] args)
        {
            new abc::ExternAlias.Lib.Class1();
            Console.WriteLine("Hello World!");
        }
    }
}
