using System;
using System.Collections.Generic;

namespace OmniSharp.Razor.Services
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