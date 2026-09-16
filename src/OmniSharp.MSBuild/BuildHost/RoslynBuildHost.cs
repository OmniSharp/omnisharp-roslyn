using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.MSBuild.ProjectFile;

namespace OmniSharp.MSBuild.BuildHost
{
    internal sealed class RoslynBuildHost : IDisposable
    {
        private const string AssemblyName = "Microsoft.CodeAnalysis.Workspaces.MSBuild";
        private const string ManagerTypeName = "Microsoft.CodeAnalysis.MSBuild.BuildHostProcessManager";
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly ImmutableDictionary<string, string> _globalProperties;
        private readonly object _manager;
        private readonly Type _managerType;
        private readonly Type _buildHostKindType;
        private readonly string _dotNetPath;
        private bool _disposed;

        public RoslynBuildHost(IReadOnlyDictionary<string, string> globalProperties, string dotNetPath, Microsoft.Extensions.Logging.ILoggerFactory loggerFactory)
        {
            _globalProperties = globalProperties.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
            _dotNetPath = dotNetPath;
            var assembly = Assembly.Load(AssemblyName);
            _managerType = RequireType(assembly, ManagerTypeName);
            _buildHostKindType = RequireType(assembly, "Microsoft.CodeAnalysis.MSBuild.BuildHostProcessKind");
            var constructor = _managerType.GetConstructors(InstanceMembers).SingleOrDefault(c => c.GetParameters().Length == 5)
                ?? throw MissingApi($"{ManagerTypeName} constructor");
            _manager = constructor.Invoke(new object[]
            {
                ImmutableArray.Create(LanguageNames.CSharp),
                _globalProperties,
                null,
                null,
                loggerFactory
            });
        }

        public BuildHostLoadResult LoadProject(string projectFilePath)
        {
            try
            {
                return LoadProjectAsync(projectFilePath, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"Roslyn BuildHost from '{AssemblyName}' could not load '{projectFilePath}'. " +
                    "OmniSharp requires the internal BuildHost API shipped with its pinned Roslyn package version.",
                    Unwrap(ex));
            }
        }

        private async Task<BuildHostLoadResult> LoadProjectAsync(string projectFilePath, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
            var getHost = RequireMethod(
                _managerType,
                "GetBuildHostAsync",
                _buildHostKindType,
                typeof(string),
                typeof(string),
                typeof(CancellationToken));
            var netCoreKind = Enum.Parse(_buildHostKindType, "NetCore");
            var host = await GetTaskResultAsync(getHost.Invoke(
                _manager,
                new[] { netCoreKind, projectFilePath, _dotNetPath, cancellationToken })).ConfigureAwait(false);

            var loadProject = RequireMethod(host.GetType(), "LoadProjectFileAsync", typeof(string), typeof(string), typeof(CancellationToken));
            var remoteProject = await GetTaskResultAsync(loadProject.Invoke(
                host,
                new object[] { projectFilePath, LanguageNames.CSharp, cancellationToken })).ConfigureAwait(false);

            try
            {
                var diagnostics = await ReadDiagnosticsAsync(remoteProject, cancellationToken).ConfigureAwait(false);
                var projects = await ReadProjectsAsync(remoteProject, cancellationToken).ConfigureAwait(false);
                var project = projects.FirstOrDefault(p => !string.IsNullOrEmpty(p.TargetFramework)) ?? projects.FirstOrDefault();

                return new BuildHostLoadResult(project, diagnostics);
            }
            finally
            {
                await DisposeAsync(remoteProject).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DisposeAsync(_manager).AsTask().GetAwaiter().GetResult();
        }

        private static async Task<ImmutableArray<BuildHostDiagnostic>> ReadDiagnosticsAsync(object remoteProject, CancellationToken cancellationToken)
        {
            var method = RequireMethod(remoteProject.GetType(), "GetDiagnosticLogItemsAsync", typeof(CancellationToken));
            var values = AsEnumerable(await GetTaskResultAsync(method.Invoke(remoteProject, new object[] { cancellationToken })).ConfigureAwait(false));

            return values.Select(value => new BuildHostDiagnostic(
                IsError: Get(value, "Kind").ToString() == "Error",
                Message: Get<string>(value, "Message"),
                ProjectFilePath: Get<string>(value, "ProjectFilePath"))).ToImmutableArray();
        }

        private async Task<ImmutableArray<BuildHostProject>> ReadProjectsAsync(object remoteProject, CancellationToken cancellationToken)
        {
            var method = RequireMethod(remoteProject.GetType(), "GetProjectFileInfosAsync", typeof(CancellationToken));
            var values = AsEnumerable(await GetTaskResultAsync(method.Invoke(remoteProject, new object[] { cancellationToken })).ConfigureAwait(false)).ToArray();
            var targetFrameworks = values.Select(value => Get<string>(value, "TargetFramework"))
                .Where(value => !string.IsNullOrWhiteSpace(value)).ToImmutableArray();

            return values.Where(value => !Get<bool>(value, "IsEmpty"))
                .Select(value => ConvertProject(value, targetFrameworks)).ToImmutableArray();
        }

        private BuildHostProject ConvertProject(object value, ImmutableArray<string> targetFrameworks)
            => new(
                Get<string>(value, "FilePath"),
                Get<string>(value, "OutputFilePath"),
                Get<string>(value, "IntermediateOutputFilePath"),
                Get<string>(value, "DefaultNamespace"),
                Get<string>(value, "TargetFramework"),
                Get<string>(value, "TargetFrameworkIdentifier"),
                Get<string>(value, "TargetFrameworkVersion"),
                _globalProperties.GetValueOrDefault(PropertyNames.Configuration) ?? "Debug",
                (_globalProperties.GetValueOrDefault(PropertyNames.Platform) ?? "Any CPU").Replace("Any CPU", "AnyCPU"),
                targetFrameworks,
                GetArray<string>(value, "CommandLineArgs"),
                GetObjects(value, "Documents", ConvertDocument),
                GetObjects(value, "AdditionalDocuments", ConvertDocument),
                GetObjects(value, "AnalyzerConfigDocuments", ConvertDocument),
                GetObjects(value, "ProjectReferences", item => new BuildHostProjectReference(
                    Get<string>(item, "Path"), GetArray<string>(item, "Aliases"), Get<bool>(item, "ReferenceOutputAssembly"))),
                GetArray<string>(value, "ProjectCapabilities"),
                GetArray<string>(value, "ContentFilePaths"),
                Get<string>(value, "ProjectAssetsFilePath"),
                GetObjects(value, "PackageReferences", item => new BuildHostPackageReference(
                    Get<string>(item, "Name"), Get<string>(item, "VersionRange"))),
                GetObjects(value, "MetadataReferences", item => new BuildHostMetadataReference(
                    Get<string>(item, "Path"), GetArray<string>(item, "Aliases"))),
                GetObjects(value, "FileGlobs", item => new BuildHostFileGlob(
                    GetArray<string>(item, "Includes"), GetArray<string>(item, "Excludes"), GetArray<string>(item, "Removes"))));

        private static BuildHostDocument ConvertDocument(object value)
            => new(Get<string>(value, "FilePath"), Get<string>(value, "LogicalPath"), Get<bool>(value, "IsGenerated"));

        private static ImmutableArray<T> GetObjects<T>(object value, string propertyName, Func<object, T> convert)
            => AsEnumerable(Get(value, propertyName)).Select(convert).ToImmutableArray();

        private static ImmutableArray<T> GetArray<T>(object value, string propertyName)
            => AsEnumerable(Get(value, propertyName)).Cast<T>().ToImmutableArray();

        private static IEnumerable<object> AsEnumerable(object value)
            => value is IEnumerable values ? values.Cast<object>() : Enumerable.Empty<object>();

        private static T Get<T>(object value, string propertyName)
            => (T)Get(value, propertyName);

        private static object Get(object value, string propertyName)
        {
            var property = value.GetType().GetProperty(propertyName, InstanceMembers)
                ?? throw MissingApi($"{value.GetType().FullName}.{propertyName}");
            return property.GetValue(value);
        }

        private static Type RequireType(Assembly assembly, string name)
            => assembly.GetType(name, throwOnError: false) ?? throw MissingApi(name);

        private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
            => type.GetMethod(name, InstanceMembers, null, parameterTypes, null)
                ?? throw MissingApi($"{type.FullName}.{name}");

        private static InvalidOperationException MissingApi(string api)
            => new($"Required Roslyn BuildHost API '{api}' was not found in {AssemblyName}.");

        private static async Task<object> GetTaskResultAsync(object taskObject)
        {
            if (taskObject is not Task task)
            {
                throw MissingApi("Task-returning RPC method");
            }

            await task.ConfigureAwait(false);
            return task.GetType().GetProperty("Result", InstanceMembers)?.GetValue(task);
        }

        private static async ValueTask DisposeAsync(object value)
        {
            if (value == null)
            {
                return;
            }

            var result = value.GetType().GetMethod("DisposeAsync", InstanceMembers)?.Invoke(value, null);
            if (result is ValueTask valueTask)
            {
                await valueTask.ConfigureAwait(false);
            }
        }

        private static Exception Unwrap(Exception exception)
            => exception is TargetInvocationException { InnerException: not null } invocationException
                ? Unwrap(invocationException.InnerException)
                : exception;

    }
}
