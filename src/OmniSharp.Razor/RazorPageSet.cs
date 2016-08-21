using System;
using System.Collections.Generic;
using OmniSharp.Razor.Services;

namespace OmniSharp.Razor
{
    public class RazorPageSet : IDisposable
    {
        public RazorPageSet(IEnumerable<RazorPageContext> pages)
        {
            Pages = pages;
        }

        public IEnumerable<RazorPageContext> Pages { get; set; }

        public void Dispose()
        {
            foreach (var page in Pages)
            {
                (page as TemporaryRazorPageContext)?.Dispose();
            }
        }
    }
}