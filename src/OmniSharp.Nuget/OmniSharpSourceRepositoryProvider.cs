using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Protocol.Core.Types;
using NuGet.Configuration;
using NuGet.Protocol.Core.v2;
using NuGet.Protocol.Core.v3;

namespace OmniSharp.NuGet
{
    public class OmniSharpSourceRepositoryProvider : ISourceRepositoryProvider
    {
        private static PackageSource[] DefaultPrimarySources = {
                new PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.FeedName, isEnabled: true, isOfficial: true)
                    {
                        Description = "Api v3",
                        ProtocolVersion = 3
                    }
            };

        private static PackageSource[] DefaultSecondarySources = {
                new PackageSource(NuGetConstants.V2FeedUrl, NuGetConstants.FeedName, isEnabled: true, isOfficial: true)
                    {
                        Description = "Api v2",
                        ProtocolVersion = 2
                    }
            };

        // TODO: add support for reloading sources when changes occur
        private readonly IPackageSourceProvider _packageSourceProvider;
        private IEnumerable<Lazy<INuGetResourceProvider>> _resourceProviders;
        private List<SourceRepository> _repositories;

        public OmniSharpSourceRepositoryProvider(string root)
        {
            var settings = global::NuGet.Configuration.Settings.LoadDefaultSettings(root: root, configFileName: null, machineWideSettings: null);
            _packageSourceProvider = new PackageSourceProvider(settings, 
                                                               migratePackageSources: null,
                                                               configurationDefaultSources: DefaultPrimarySources.Concat(DefaultSecondarySources));
            _resourceProviders = Repository.Provider.GetCoreV3()
                .Concat(Repository.Provider.GetCoreV2());
            _repositories = new List<SourceRepository>();

            // Refresh the package sources
            Init();

            // Hook up event to refresh package sources when the package sources changed
            _packageSourceProvider.PackageSourcesChanged += (sender, e) => { Init(); };
        }

        public IEnumerable<SourceRepository> GetRepositories()
        {
            return _repositories;
        }

        /// <summary>
        /// Create a repository for one time use.
        /// </summary>
        public SourceRepository CreateRepository(PackageSource source)
        {
            return new SourceRepository(source, _resourceProviders);
        }

        public IPackageSourceProvider PackageSourceProvider
        {
            get { return _packageSourceProvider; }
        }

        private void Init()
        {
            _repositories.Clear();
            foreach (var source in _packageSourceProvider.LoadPackageSources())
            {
                if (source.IsEnabled)
                {
                    var sourceRepo = new SourceRepository(source, _resourceProviders);
                    _repositories.Add(sourceRepo);
                }
            }
        }
    }
}
