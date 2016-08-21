using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Razor.Directives;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.FileProviders;
using OmniSharp.Services;

namespace OmniSharp.Razor.Services
{
    [Export]
    public class RazorPageActivator
    {
        private readonly IOmnisharpEnvironment _environment;
        private ConcurrentDictionary<string, object> _razorPages;

        [ImportingConstructor]

        public RazorPageActivator(IOmnisharpEnvironment environment)
        {
            _environment = environment;
            _razorPages = new ConcurrentDictionary<string, object>();
        }

        public void OpenPage(string filePath)
        {
            // You guys... makeing the constructor internal just means we have extra hoops to go through...
            //     it won't stop nice guys from doing bad things...

            typeof (MvcRazorHost).GetTypeInfo().GetConstructors(BindingFlags.NonPublic)
                .Where

            var host = new MvcRazorHost(new DefaultChunkTreeCache(new PhysicalFileProvider(_environment.Path)),
                new DesignTimeRazorPathNormalizer(_environment.Path));
        }

        public void ClosePage(string filePath)
        {

        }
    }
}
