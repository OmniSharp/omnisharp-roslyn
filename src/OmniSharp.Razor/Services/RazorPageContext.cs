using System;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Razor.Services
{
    public class RazorPageContext
    {
        public RazorPageContext(DocumentId documentId, MvcRazorHost host)
        {
            DocumentId = documentId;
            Host = host;
            Buffer = String.Empty;
        }

        public MvcRazorHost Host { get; }
        public DocumentId DocumentId{ get; }
        public string Buffer { get; set; }
    }

    public class TemporaryRazorPageContext : RazorPageContext, IDisposable
    {
        private readonly Action _disposer;

        public TemporaryRazorPageContext(DocumentId documentId, MvcRazorHost host, Action disposer) : base(documentId, host)
        {
            _disposer = disposer;
        }

        public void Dispose()
        {
            _disposer();
        }
    }
}