using System;
using Microsoft.CodeAnalysis;
using System.IO;
using OmniSharp.Services;

namespace OmniSharp.MSBuild
{
    public static class AssemblySearch
    {
        public static readonly string[] Paths =
        {
            //Windows Paths
            @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5",
            @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0",
            @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\v3.5",
            @"C:\Program Files\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5",
            @"C:\Program Files\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0",
            @"C:\Program Files\Reference Assemblies\Microsoft\Framework\v3.5",
            @"C:\Windows\Microsoft.NET\Framework\v2.0.50727",
            @"C:\Program Files (x86)\Microsoft ASP.NET\ASP.NET Web Pages\v2.0\Assemblies",
            @"C:\Program Files (x86)\Microsoft ASP.NET\ASP.NET Web Pages\v1.0\Assemblies",
            @"C:\Program Files (x86)\Microsoft ASP.NET\ASP.NET MVC 4\Assemblies",
            @"C:\Program Files (x86)\Microsoft ASP.NET\ASP.NET MVC 3\Assemblies",
            @"C:\Program Files\Microsoft ASP.NET\ASP.NET Web Pages\v2.0\Assemblies",
            @"C:\Program Files\Microsoft ASP.NET\ASP.NET Web Pages\v1.0\Assemblies",
            @"C:\Program Files\Microsoft ASP.NET\ASP.NET MVC 4\Assemblies",
            @"C:\Program Files\Microsoft ASP.NET\ASP.NET MVC 3\Assemblies",
            @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\IDE\ReferenceAssemblies\v4.5",
            @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\IDE\ReferenceAssemblies\v4.0",
            @"C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\IDE\ReferenceAssemblies\v2.0",
            @"C:\Program Files (x86)\Microsoft Visual Studio 10.0\Common7\IDE\ReferenceAssemblies\v2.0",
            @"C:\Program Files (x86)\Microsoft Visual Studio 9.0\Common7\IDE\PublicAssemblies",
            @"C:\Program Files\Microsoft Visual Studio 11.0\Common7\IDE\ReferenceAssemblies\v4.5",
            @"C:\Program Files\Microsoft Visual Studio 11.0\Common7\IDE\ReferenceAssemblies\v4.0",
            @"C:\Program Files\Microsoft Visual Studio 11.0\Common7\IDE\ReferenceAssemblies\v2.0",
            @"C:\Program Files\Microsoft Visual Studio 10.0\Common7\IDE\ReferenceAssemblies\v2.0",
            @"C:\Program Files\Microsoft Visual Studio 9.0\Common7\IDE\PublicAssemblies",

            //Unix Paths
            @"/usr/local/lib/mono/4.5",
            @"/usr/local/lib/mono/4.0",
            @"/usr/local/lib/mono/3.5",
            @"/usr/local/lib/mono/2.0",
            @"/usr/lib/mono/4.5",
            @"/usr/lib/mono/4.0",
            @"/usr/lib/mono/3.5",
            @"/usr/lib/mono/2.0",
            @"/opt/mono/lib/mono/4.5",
            @"/opt/mono/lib/mono/4.0",
            @"/opt/mono/lib/mono/3.5",
            @"/opt/mono/lib/mono/2.0",

            //OS X Paths
            @"/Library/Frameworks/Mono.Framework/Libraries/mono/4.5",
            @"/Library/Frameworks/Mono.Framework/Libraries/mono/4.0",
            @"/Library/Frameworks/Mono.Framework/Libraries/mono/3.5",
            @"/Library/Frameworks/Mono.Framework/Libraries/mono/2.0",
            @"~/.kpm/packages"
        };
    }

    public class StupidReferenceResolver
    {
        private string _projectDirectory;

        public StupidReferenceResolver(string projectDirectory)
        {
            _projectDirectory = projectDirectory;
        }

        public string Resolve(string evaluatedInclude, string hintPath = null)
        {   
            if (evaluatedInclude.IndexOf(',') >= 0)
            {
                evaluatedInclude = evaluatedInclude.Substring(0, evaluatedInclude.IndexOf(','));
            }

            if(!string.IsNullOrWhiteSpace(hintPath))
            {
                var hintedAssemblyFile = Path.Combine(_projectDirectory, hintPath);
                if(File.Exists(hintedAssemblyFile))
                {
                    return hintedAssemblyFile;
                }
            }

            string directAssemblyFile = evaluatedInclude + ".dll";
            if (File.Exists(directAssemblyFile))
            {
                return directAssemblyFile;
            }

            foreach (string searchPath in AssemblySearch.Paths)
            {
                string assemblyFile = Path.Combine(searchPath, evaluatedInclude + ".dll");
                if (File.Exists(assemblyFile))
                    return assemblyFile;
            }

            return null;
        }
    }
}