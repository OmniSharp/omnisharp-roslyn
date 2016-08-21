using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.AspNetCore.Mvc.Razor.Directives;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp.Razor.Services;
using OmniSharp.Razor.Workers;
using OmniSharp.Services;

namespace OmniSharp.Razor
{
    [Export, Shared]
    public class RazorWorkspace
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IOmnisharpEnvironment _environment;
        private readonly OmnisharpWorkspace _workspace;
        private readonly ConcurrentDictionary<DocumentId, RazorPageContext> _razorPages;

        [ImportingConstructor]
        public RazorWorkspace(IOmnisharpEnvironment environment, OmnisharpWorkspace workspace, ILoggerFactory loggerFactory)
            : this(environment, workspace, new PhysicalFileProvider(environment.Path), loggerFactory)
        {
        }

        internal RazorWorkspace(IOmnisharpEnvironment environment, OmnisharpWorkspace workspace, IFileProvider fileProvider, ILoggerFactory loggerFactory)
        {
            _environment = environment;
            _workspace = workspace;
            FileProvider = fileProvider;
            Path = _environment.Path;
            _loggerFactory = loggerFactory;
            _razorPages = new ConcurrentDictionary<DocumentId, RazorPageContext>();

            _workspace.DocumentOpened += WorkspaceOnDocumentOpened;
            _workspace.DocumentClosed += WorkspaceOnDocumentClosed;
        }

        private void WorkspaceOnDocumentOpened(object sender, DocumentEventArgs documentEventArgs)
        {
            if (documentEventArgs.Document.Project.Language == RazorLanguage.Razor)
            {
                var options = new OptionsWrapper<RazorViewEngineOptions>(new RazorViewEngineOptions()
                {
                    FileProviders = { FileProvider },
                    CompilationCallback = ctx =>
                    {
                        // TODO: Come and change this...
                        ctx.Compilation =
                        ctx.Compilation.AddReferences(
                            _workspace.CurrentSolution.Projects.First().MetadataReferences.First());
                    }
                });
                var host = this.CreateHost();
                var compilationService = CreateCompilationService(options);
                var razorCompilationService = CreateRazorCompilationService(compilationService, host);
                var context = new RazorPageContext(documentEventArgs.Document.Id, host, compilationService, razorCompilationService);
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

        private ICompilationService CreateCompilationService(IOptions<RazorViewEngineOptions> options)
        {
            return new DefaultRoslynCompilationService(
                new ApplicationPartManager(),
                options,
                new DefaultRazorViewEngineFileProviderAccessor(options),
                _loggerFactory);
        }

        private IRazorCompilationService CreateRazorCompilationService(ICompilationService compilationService, MvcRazorHost host)
        {
            return new RazorCompilationService(
                compilationService,
                host,
                new OmnisharpRazorViewEngineFileProviderAccessor(FileProvider),
                _loggerFactory);
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
                    var options = new OptionsWrapper<RazorViewEngineOptions>(new RazorViewEngineOptions()
                    {
                        FileProviders = { FileProvider },
                        CompilationCallback = context =>
                        {
                            // TODO: Come and change this...
                            context.Compilation =
                                context.Compilation.AddReferences(
                                    _workspace.CurrentSolution.Projects.First().MetadataReferences.First());
                        }
                    });
                    var host = this.CreateHost();
                    var compilationService = CreateCompilationService(options);
                    var razorCompilationService = CreateRazorCompilationService(compilationService, host);
                    pages.Add(new TemporaryRazorPageContext(filePath, host, compilationService, razorCompilationService, () => { }));
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
