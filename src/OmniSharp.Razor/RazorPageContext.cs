using System;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Razor
{
    public class RazorPageContext
    {
        public RazorPageContext(DocumentId documentId, MvcRazorHost host, ICompilationService compilationService, IRazorCompilationService razorCompilationService)
        {
            DocumentId = documentId;
            Host = host;
            CompilationService = compilationService;
            RazorCompilationService = razorCompilationService;
            Buffer = String.Empty;
        }

        public MvcRazorHost Host { get; }
        public DocumentId DocumentId{ get; }
        public ICompilationService CompilationService { get; }
        public IRazorCompilationService RazorCompilationService { get; }
        public string Buffer { get; set; }
    }

    public class TemporaryRazorPageContext : RazorPageContext, IDisposable
    {
        private readonly Action _disposer;

        public TemporaryRazorPageContext(DocumentId documentId, MvcRazorHost host, ICompilationService compilationService, IRazorCompilationService razorCompilationService, Action disposer) : base(documentId, host, compilationService, razorCompilationService)
        {
            _disposer = disposer;
        }

        public void Dispose()
        {
            _disposer();
        }
    }
}