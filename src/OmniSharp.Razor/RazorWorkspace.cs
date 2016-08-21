using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Razor.Directives;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.FileProviders;
using OmniSharp.Razor.Services;
using OmniSharp.Razor.Workers;
using OmniSharp.Services;

namespace OmniSharp.Razor
{

    [Export, Shared]
    public class RazorWorkspace
    {
        private readonly IOmnisharpEnvironment _environment;
        private readonly OmnisharpWorkspace _workspace;
        private readonly ConcurrentDictionary<DocumentId, RazorPageContext> _razorPages;

        [ImportingConstructor]
        public RazorWorkspace(IOmnisharpEnvironment environment, OmnisharpWorkspace workspace)
            : this(environment, workspace, new PhysicalFileProvider(System.IO.Directory.GetCurrentDirectory()))
        {
        }

        internal RazorWorkspace(IOmnisharpEnvironment environment, OmnisharpWorkspace workspace, IFileProvider fileProvider)
        {
            _environment = environment;
            _workspace = workspace;
            FileProvider = fileProvider;
            Path = _environment.Path;
            _razorPages = new ConcurrentDictionary<DocumentId, RazorPageContext>();

            _workspace.DocumentOpened += WorkspaceOnDocumentOpened;
            _workspace.DocumentClosed += WorkspaceOnDocumentClosed;
        }

        private void WorkspaceOnDocumentOpened(object sender, DocumentEventArgs documentEventArgs)
        {
            if (documentEventArgs.Document.Project.Language == RazorLanguage.Razor)
            {
                var host = this.CreateHost();
                var context = new RazorPageContext(documentEventArgs.Document.Id, host);
                this._razorPages.TryAdd(documentEventArgs.Document.Id, context);
            }
        }

        private void WorkspaceOnDocumentClosed(object sender, DocumentEventArgs documentEventArgs)
        {
            if (documentEventArgs.Document.Project.Language == RazorLanguage.Razor)
            {
                RazorPageContext context;
                this._razorPages.TryRemove(documentEventArgs.Document.Id, out context);
            }
        }

        public IEnumerable<DocumentId> OpenDocumentIds => this._razorPages.Keys;

        public string Path { get; }

        public IFileProvider FileProvider { get; }

        private MvcRazorHost CreateHost()
        {
            // You guys... making the constructor internal doesn't stop us...
            //     it just slows us down...
            var ctor = typeof(MvcRazorHost).GetTypeInfo()
                .DeclaredConstructors
                .Where(x => x.GetParameters().Length == 2)
                .Where(x => x.GetParameters()[0].ParameterType == typeof(IChunkTreeCache))
                .Single(x => x.GetParameters()[1].ParameterType == typeof(RazorPathNormalizer));

            var host = (MvcRazorHost)ctor.Invoke(new object[]
            {
                    new DefaultChunkTreeCache(FileProvider),
                    new DesignTimeRazorPathNormalizer(Path)
            });
            host.DesignTimeMode = true;
            return host;
        }

        public RazorPageSet OpenPageSet(params DocumentId[] documents)
        {
            var pages = new List<RazorPageContext>();
            foreach (var filePath in documents)
            {
                if (_razorPages.ContainsKey(filePath))
                {
                    pages.Add(_razorPages[filePath]);
                }
                else
                {
                    var host = CreateHost();
                    pages.Add(new TemporaryRazorPageContext(filePath, host, () => { }));
                }
            }
            return new RazorPageSet(pages);
        }

        public RazorPageSet OpenAllPages() => OpenPageSet(
            _workspace.CurrentSolution
                .Projects.Where(z => z.Language == RazorLanguage.Razor)
                .SelectMany(z => z.DocumentIds)
                .ToArray());
    }
}
