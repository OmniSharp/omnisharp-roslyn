// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
// This is simplified version from roslyn codebase, originated from https://github.com/dotnet/roslyn/blob/master/src/Compilers/Shared/ShadowCopyAnalyzerAssemblyLoader.cs

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Utilities;

namespace OmniSharp.Host.Services
{
    // This is shadow copying loader. Makes sure that analyzer assemblies are not locked
    // on disk during analysis.
    public class AnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
    {
        private readonly string _baseDirectory;
        private readonly Lazy<string> _shadowCopyDirectoryAndMutex;
        private int _assemblyDirectoryId;

        private readonly object _guard = new object();

        private readonly Dictionary<string, Assembly> _loadedAssembliesByPath = new Dictionary<string, Assembly>();
        private readonly Dictionary<string, AssemblyIdentity> _loadedAssemblyIdentitiesByPath = new Dictionary<string, AssemblyIdentity>();
        private readonly Dictionary<AssemblyIdentity, Assembly> _loadedAssembliesByIdentity = new Dictionary<AssemblyIdentity, Assembly>();
        private readonly Dictionary<string, HashSet<string>> _knownAssemblyPathsBySimpleName = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private int _hookedAssemblyResolve;

        public AnalyzerAssemblyLoader()
        {
            _baseDirectory = Path.Combine(Path.GetTempPath(), "CodeAnalysis", "AnalyzerShadowCopies");

            _shadowCopyDirectoryAndMutex = new Lazy<string>(
                () => CreateUniqueDirectoryForProcess(), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public void AddDependencyLocation(string fullPath)
        {
            string simpleName = Path.GetFileNameWithoutExtension(fullPath);

            lock (_guard)
            {
                if (!_knownAssemblyPathsBySimpleName.TryGetValue(simpleName, out var paths))
                {
                    paths = new HashSet<string>();
                    _knownAssemblyPathsBySimpleName.Add(simpleName, paths);
                }

                paths.Add(fullPath);
            }
        }

        public Assembly LoadFromPath(string fullPath)
        {
            return LoadFromPathUncheckedCore(fullPath);
        }

        private Assembly LoadFromPathUncheckedCore(string fullPath, AssemblyIdentity identity = null)
        {

            // Check if we have already loaded an assembly with the same identity or from the given path.
            Assembly loadedAssembly = null;
            lock (_guard)
            {
                if (_loadedAssembliesByPath.TryGetValue(fullPath, out var existingAssembly))
                {
                    loadedAssembly = existingAssembly;
                }
                else
                {
                    identity = identity ?? GetOrAddAssemblyIdentity(fullPath);
                    if (identity != null && _loadedAssembliesByIdentity.TryGetValue(identity, out existingAssembly))
                    {
                        loadedAssembly = existingAssembly;
                    }
                }
            }

            // Otherwise, load the assembly.
            if (loadedAssembly == null)
            {
                loadedAssembly = LoadFromPathImpl(fullPath);
            }

            // Add the loaded assembly to both path and identity cache.
            return AddToCache(loadedAssembly, fullPath, identity);
        }

        private AssemblyIdentity GetOrAddAssemblyIdentity(string fullPath)
        {
            lock (_guard)
            {
                if (_loadedAssemblyIdentitiesByPath.TryGetValue(fullPath, out var existingIdentity))
                {
                    return existingIdentity;
                }
            }

            var identity = TryGetAssemblyIdentity(fullPath);
            return AddToCache(fullPath, identity);
        }

        private Assembly AddToCache(Assembly assembly, string fullPath, AssemblyIdentity identity)
        {
            identity = AddToCache(fullPath, identity ?? AssemblyIdentity.FromAssemblyDefinition(assembly));

            lock (_guard)
            {
                // The same assembly may be loaded from two different full paths (e.g. when loaded from GAC, etc.),
                // or another thread might have loaded the assembly after we checked above.
                if (_loadedAssembliesByIdentity.TryGetValue(identity, out var existingAssembly))
                {
                    assembly = existingAssembly;
                }
                else
                {
                    _loadedAssembliesByIdentity.Add(identity, assembly);
                }

                // An assembly file might be replaced by another file with a different identity.
                // Last one wins.
                _loadedAssembliesByPath[fullPath] = assembly;

                return assembly;
            }
        }

        private AssemblyIdentity AddToCache(string fullPath, AssemblyIdentity identity)
        {
            lock (_guard)
            {
                if (_loadedAssemblyIdentitiesByPath.TryGetValue(fullPath, out var existingIdentity) && existingIdentity != null)
                {
                    identity = existingIdentity;
                }
                else
                {
                    _loadedAssemblyIdentitiesByPath[fullPath] = identity;
                }
            }

            return identity;
        }

        private static string CopyFileAndResources(string fullPath, string assemblyDirectory)
        {
            string fileNameWithExtension = Path.GetFileName(fullPath);
            string shadowCopyPath = Path.Combine(assemblyDirectory, fileNameWithExtension);

            CopyFile(fullPath, shadowCopyPath);

            string originalDirectory = Path.GetDirectoryName(fullPath);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileNameWithExtension);
            string resourcesNameWithoutExtension = fileNameWithoutExtension + ".resources";
            string resourcesNameWithExtension = resourcesNameWithoutExtension + ".dll";

            foreach (var directory in Directory.EnumerateDirectories(originalDirectory))
            {
                string directoryName = Path.GetFileName(directory);

                string resourcesPath = Path.Combine(directory, resourcesNameWithExtension);
                if (File.Exists(resourcesPath))
                {
                    string resourcesShadowCopyPath = Path.Combine(assemblyDirectory, directoryName, resourcesNameWithExtension);
                    CopyFile(resourcesPath, resourcesShadowCopyPath);
                }

                resourcesPath = Path.Combine(directory, resourcesNameWithoutExtension, resourcesNameWithExtension);
                if (File.Exists(resourcesPath))
                {
                    string resourcesShadowCopyPath = Path.Combine(assemblyDirectory, directoryName, resourcesNameWithoutExtension, resourcesNameWithExtension);
                    CopyFile(resourcesPath, resourcesShadowCopyPath);
                }
            }

            return shadowCopyPath;
        }

        private static void CopyFile(string originalPath, string shadowCopyPath)
        {
            var directory = Path.GetDirectoryName(shadowCopyPath);
            Directory.CreateDirectory(directory);

            File.Copy(originalPath, shadowCopyPath);

            ClearReadOnlyFlagOnFile(new FileInfo(shadowCopyPath));
        }

        private static void ClearReadOnlyFlagOnFile(FileInfo fileInfo)
        {
            try
            {
                if (fileInfo.IsReadOnly)
                {
                    fileInfo.IsReadOnly = false;
                }
            }
            catch
            {
                // There are many reasons this could fail. Ignore it and keep going.
            }
        }

        private string CreateUniqueDirectoryForAssembly()
        {
            int directoryId = Interlocked.Increment(ref _assemblyDirectoryId);

            string directory = Path.Combine(_shadowCopyDirectoryAndMutex.Value, directoryId.ToString());

            Directory.CreateDirectory(directory);
            return directory;
        }

        private string CreateUniqueDirectoryForProcess()
        {
            string guid = Guid.NewGuid().ToString("N").ToLowerInvariant();
            string directory = Path.Combine(_baseDirectory, guid);

            Directory.CreateDirectory(directory);
            return directory;
        }

        private Assembly LoadFromPathImpl(string originalPath)
        {
            string assemblyDirectory = CreateUniqueDirectoryForAssembly();
            string shadowCopyPath = CopyFileAndResources(originalPath, assemblyDirectory);

            if (Interlocked.CompareExchange(ref _hookedAssemblyResolve, value: 1, comparand: 0) == 0)
            {
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            }

            return Assembly.LoadFrom(shadowCopyPath);
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                return Load(AppDomain.CurrentDomain.ApplyPolicy(args.Name));
            }
            catch
            {
                return null;
            }
        }

        public Assembly Load(string displayName)
        {
            if (!AssemblyIdentity.TryParseDisplayName(displayName, out var requestedIdentity))
            {
                return null;
            }

            ImmutableArray<string> candidatePaths;
            lock (_guard)
            {

                // First, check if this loader already loaded the requested assembly:
                if (_loadedAssembliesByIdentity.TryGetValue(requestedIdentity, out var existingAssembly))
                {
                    return existingAssembly;
                }
                // Second, check if an assembly file of the same simple name was registered with the loader:
                if (!_knownAssemblyPathsBySimpleName.TryGetValue(requestedIdentity.Name, out var pathList))
                {
                    return null;
                }

                candidatePaths = pathList.ToImmutableArray();
            }

            // Multiple assemblies of the same simple name but different identities might have been registered.
            // Load the one that matches the requested identity (if any).
            foreach (var candidatePath in candidatePaths)
            {
                var candidateIdentity = GetOrAddAssemblyIdentity(candidatePath);

                if (requestedIdentity.Equals(candidateIdentity))
                {
                    return LoadFromPathUncheckedCore(candidatePath, candidateIdentity);
                }
            }

            return null;
        }

        private static AssemblyIdentity TryGetAssemblyIdentity(string filePath)
        {
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var peReader = new PEReader(stream))
                {
                    var metadataReader = peReader.GetMetadataReader();

                    AssemblyDefinition assemblyDefinition = metadataReader.GetAssemblyDefinition();

                    string name = metadataReader.GetString(assemblyDefinition.Name);
                    Version version = assemblyDefinition.Version;

                    StringHandle cultureHandle = assemblyDefinition.Culture;
                    string cultureName = (!cultureHandle.IsNil) ? metadataReader.GetString(cultureHandle) : null;
                    AssemblyFlags flags = assemblyDefinition.Flags;

                    bool hasPublicKey = (flags & AssemblyFlags.PublicKey) != 0;
                    BlobHandle publicKeyHandle = assemblyDefinition.PublicKey;
                    ImmutableArray<byte> publicKeyOrToken = !publicKeyHandle.IsNil
                        ? metadataReader.GetBlobBytes(publicKeyHandle).AsImmutableOrNull()
                        : default;
                    return new AssemblyIdentity(name, version, cultureName, publicKeyOrToken, hasPublicKey);
                }
            }
            catch { }

            return null;
        }
    }
}