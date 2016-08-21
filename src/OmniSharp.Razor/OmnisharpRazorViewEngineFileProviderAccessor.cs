using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OmniSharp.Razor
{
    public class OmnisharpRazorViewEngineFileProviderAccessor : IRazorViewEngineFileProviderAccessor
    {
        public OmnisharpRazorViewEngineFileProviderAccessor(IFileProvider fileProvider)
        {
            FileProvider = fileProvider;
        }

        public IFileProvider FileProvider { get; }
    }
}