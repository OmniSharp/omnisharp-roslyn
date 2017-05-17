using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NuGet.ProjectModel;
using OmniSharp.MSBuild.ProjectFile;

namespace OmniSharp.MSBuild.Resolution
{
    internal class PackageDependencyResolver
    {
        private readonly ILogger _logger;

        public PackageDependencyResolver(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<PackageDependencyResolver>();
        }

        public ImmutableArray<PackageReference> FindUnresolvedPackageReferences(ProjectFileInfo projectFile)
        {
            if (projectFile.PackageReferences.Length == 0)
            {
                return ImmutableArray<PackageReference>.Empty;
            }

            // If the lock file does not exist, all of the package references are unresolved.
            if (!File.Exists(projectFile.ProjectAssetsFile))
            {
                return projectFile.PackageReferences;
            }

            var lockFileFormat = new LockFileFormat();
            var lockFile = lockFileFormat.Read(projectFile.ProjectAssetsFile);

            return FindUnresolvedPackageReferencesInLockFile(projectFile, lockFile);
        }

        private ImmutableArray<PackageReference> FindUnresolvedPackageReferencesInLockFile(ProjectFileInfo projectFile, LockFile lockFile)
        {
            var libraryMap = CreateLibraryMap(lockFile);

            var unresolved = ImmutableArray.CreateBuilder<PackageReference>();

            // Iterate through each package reference and see if we can find a library with the same name
            // that satisfies the reference's version range in the lock file.

            foreach (var reference in projectFile.PackageReferences)
            {
                if (!libraryMap.TryGetValue(reference.Dependency.Id, out var libraries))
                {
                    _logger.LogWarning($"{projectFile.Name}: Did not find '{reference.Dependency.Id}' in lock file.");
                    unresolved.Add(reference);
                }
                else
                {
                    var found = false;
                    foreach (var library in libraries)
                    {
                        if (reference.Dependency.VersionRange.Satisfies(library.Version))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        var referenceText = reference.IsImplicitlyDefined
                            ? "implicit package reference"
                            : "package reference";

                        var versions = string.Join(", ", libraries.Select(l => '"' + l.Version.ToString() + '"'));

                        _logger.LogWarning($"{projectFile.Name}: Found {referenceText} '{reference.Dependency.Id}', but none of the versions in the lock file ({versions}) satisfy {reference.Dependency.VersionRange}");
                        unresolved.Add(reference);
                    }
                }
            }

            return unresolved.ToImmutable();
        }

        private static Dictionary<string, List<LockFileLibrary>> CreateLibraryMap(LockFile lockFile)
        {
            // Create map of all libraries in the lock file by their name.
            // Note that the map's key is case-insensitive.

            var libraryMap = new Dictionary<string, List<LockFileLibrary>>(
                capacity: lockFile.Libraries.Count,
                comparer: StringComparer.OrdinalIgnoreCase);

            foreach (var library in lockFile.Libraries)
            {
                if (!libraryMap.TryGetValue(library.Name, out var libraries))
                {
                    libraries = new List<LockFileLibrary>();
                    libraryMap.Add(library.Name, libraries);
                }

                libraries.Add(library);
            }

            return libraryMap;
        }
    }
}
