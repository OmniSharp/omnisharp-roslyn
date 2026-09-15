using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Roslyn.Reflection
{
    internal static class RoslynReflection
    {
        internal const string CodeAnalysisAssembly = "Microsoft.CodeAnalysis";
        internal const string WorkspacesAssembly = "Microsoft.CodeAnalysis.Workspaces";
        internal const string CSharpWorkspacesAssembly = "Microsoft.CodeAnalysis.CSharp.Workspaces";
        internal const string FeaturesAssembly = "Microsoft.CodeAnalysis.Features";
        internal const string CSharpFeaturesAssembly = "Microsoft.CodeAnalysis.CSharp.Features";

        private const BindingFlags AllMembers =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private static readonly Dictionary<(string AssemblyName, string TypeName), Type> s_types = new();
        private static readonly Dictionary<(Type Type, string Name, MethodInfo Predicate), MethodInfo> s_methods = new();
        private static readonly Dictionary<(Type Type, string Name), PropertyInfo> s_properties = new();
        private static readonly object s_gate = new();

        public static Type GetType(string assemblyName, string typeName)
        {
            lock (s_gate)
            {
                if (!s_types.TryGetValue((assemblyName, typeName), out var type))
                {
                    var assembly = Assembly.Load(new AssemblyName(assemblyName));
                    type = assembly.GetType(typeName, throwOnError: false)
                        ?? throw new InvalidOperationException(
                            $"Roslyn {assemblyName} does not contain the expected internal type '{typeName}'.");
                    s_types.Add((assemblyName, typeName), type);
                }

                return type;
            }
        }

        public static MethodInfo GetMethod(
            Type type,
            string methodName,
            int parameterCount,
            bool isStatic,
            int genericArgumentCount = 0)
        {
            var methods = type.GetMethods(AllMembers)
                .Where(method =>
                    method.Name == methodName &&
                    method.IsStatic == isStatic &&
                    method.GetParameters().Length == parameterCount &&
                    method.GetGenericArguments().Length == genericArgumentCount)
                .ToArray();

            return methods.Length switch
            {
                1 => methods[0],
                0 => throw new InvalidOperationException(
                    $"Roslyn type '{type.FullName}' does not contain the expected method " +
                    $"'{methodName}' with {parameterCount} parameters."),
                _ => throw new InvalidOperationException(
                    $"Roslyn type '{type.FullName}' contains multiple matching overloads of " +
                    $"'{methodName}' with {parameterCount} parameters.")
            };
        }

        public static MethodInfo GetMethod(Type type, string methodName, Func<MethodInfo, bool> predicate)
        {
            lock (s_gate)
            {
                var key = (type, methodName, predicate.Method);
                if (s_methods.TryGetValue(key, out var cached))
                    return cached;

                var methods = type.GetMethods(AllMembers)
                    .Where(method => method.Name == methodName && predicate(method))
                    .ToArray();
                var result = methods.Length switch
                {
                    1 => methods[0],
                    0 => throw new InvalidOperationException(
                        $"Roslyn type '{type.FullName}' does not contain the expected method '{methodName}'."),
                    _ => throw new InvalidOperationException(
                        $"Roslyn type '{type.FullName}' contains multiple matching overloads of '{methodName}'.")
                };
                s_methods.Add(key, result);
                return result;
            }
        }

        public static ConstructorInfo GetConstructor(Type type, int parameterCount)
        {
            var constructors = type.GetConstructors(AllMembers)
                .Where(constructor => constructor.GetParameters().Length == parameterCount)
                .ToArray();

            return constructors.Length switch
            {
                1 => constructors[0],
                0 => throw new InvalidOperationException(
                    $"Roslyn type '{type.FullName}' does not contain an expected constructor " +
                    $"with {parameterCount} parameters."),
                _ => throw new InvalidOperationException(
                    $"Roslyn type '{type.FullName}' contains multiple constructors with " +
                    $"{parameterCount} parameters.")
            };
        }

        public static object CreateInstance(Type type, params object[] arguments)
            => Invoke(() => GetConstructor(type, arguments.Length).Invoke(arguments));

        public static object CreateWithProperties(
            Type type,
            object defaultValue,
            params (string Name, object Value)[] properties)
        {
            var instance = defaultValue ?? Activator.CreateInstance(type, nonPublic: true);
            foreach (var (name, value) in properties)
            {
                var property = type.GetProperty(name, AllMembers)
                    ?? throw new InvalidOperationException(
                        $"Roslyn type '{type.FullName}' does not contain the expected property '{name}'.");
                property.SetValue(instance, ConvertValue(value, property.PropertyType));
            }

            return instance;
        }

        public static object GetStaticProperty(Type type, string propertyName)
            => GetProperty(type, propertyName).GetValue(null);

        public static object GetPropertyValue(object instance, string propertyName)
            => GetProperty(instance.GetType(), propertyName).GetValue(instance);

        public static T GetPropertyValue<T>(object instance, string propertyName)
            => (T)GetPropertyValue(instance, propertyName);

        public static object GetFieldValue(object instance, string fieldName)
        {
            var field = instance.GetType().GetField(fieldName, AllMembers)
                ?? throw new InvalidOperationException(
                    $"Roslyn type '{instance.GetType().FullName}' does not contain the expected field '{fieldName}'.");
            return field.GetValue(instance);
        }

        public static void SetPropertyValue(object instance, string propertyName, object value)
        {
            var property = GetProperty(instance.GetType(), propertyName);
            property.SetValue(instance, ConvertValue(value, property.PropertyType));
        }

        public static object Invoke(MethodInfo method, object instance, params object[] arguments)
            => Invoke(() => method.Invoke(instance, arguments));

        public static object Invoke(ConstructorInfo constructor, params object[] arguments)
            => Invoke(() => constructor.Invoke(arguments));

        public static T Invoke<T>(MethodInfo method, object instance, params object[] arguments)
            => (T)Invoke(method, instance, arguments);

        public static async Task<object> AwaitResultAsync(object awaitable)
        {
            if (awaitable is Task task)
            {
                await task.ConfigureAwait(false);
                return task.GetType().GetProperty("Result", AllMembers)?.GetValue(task);
            }

            var asTask = awaitable.GetType().GetMethod("AsTask", AllMembers, null, Type.EmptyTypes, null)
                ?? throw new InvalidOperationException(
                    $"Roslyn returned unsupported awaitable type '{awaitable.GetType().FullName}'.");
            return await AwaitResultAsync(Invoke(asTask, awaitable)).ConfigureAwait(false);
        }

        public static async Task<T> AwaitResultAsync<T>(object awaitable)
            => (T)await AwaitResultAsync(awaitable).ConfigureAwait(false);

        public static object CreateImmutableArray(Type elementType, IEnumerable values)
        {
            var createRange = typeof(ImmutableArray).GetMethods(AllMembers)
                .Single(method => method.Name == nameof(ImmutableArray.CreateRange) &&
                                  method.IsGenericMethodDefinition &&
                                  method.GetParameters().Length == 1 &&
                                  method.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            var cast = typeof(Enumerable).GetMethod(nameof(Enumerable.Cast), AllMembers)
                .MakeGenericMethod(elementType)
                .Invoke(null, new object[] { values });
            return Invoke(createRange.MakeGenericMethod(elementType), null, cast);
        }

        public static IEnumerable<object> Enumerate(object sequence)
        {
            if (sequence is not IEnumerable enumerable)
            {
                throw new InvalidOperationException(
                    $"Roslyn returned non-enumerable type '{sequence?.GetType().FullName ?? "<null>"}'.");
            }

            foreach (var item in enumerable)
            {
                yield return item;
            }
        }

        public static object GetRequiredLanguageService(
            Document document,
            string serviceAssemblyName,
            string serviceTypeName)
        {
            var serviceType = GetType(serviceAssemblyName, serviceTypeName);
            var languageServices = document.Project.Services;
            var getService = languageServices.GetType().GetMethods(AllMembers)
                .Single(method =>
                    method.Name == "GetService" &&
                    method.IsGenericMethodDefinition &&
                    method.GetParameters().Length == 0);
            var service = Invoke(getService.MakeGenericMethod(serviceType), languageServices);
            return service ?? throw new InvalidOperationException(
                $"No Roslyn language service '{serviceTypeName}' is available for '{document.Project.Language}'.");
        }

        public static object ConvertValue(object value, Type targetType)
        {
            if (value is null || targetType.IsInstanceOfType(value))
            {
                return value;
            }

            if (targetType.IsEnum)
            {
                return value is string name
                    ? Enum.Parse(targetType, name)
                    : Enum.ToObject(targetType, value);
            }

            return Convert.ChangeType(value, targetType);
        }

        private static PropertyInfo GetProperty(Type type, string propertyName)
        {
            lock (s_gate)
            {
                if (s_properties.TryGetValue((type, propertyName), out var cached))
                    return cached;

                var properties = type.GetProperties(AllMembers).Where(p => p.Name == propertyName).ToArray();
                var result = properties.FirstOrDefault(p => p.DeclaringType == type)
                    ?? properties.FirstOrDefault()
                    ?? throw new InvalidOperationException(
                        $"Roslyn type '{type.FullName}' does not contain the expected property '{propertyName}'.");
                s_properties.Add((type, propertyName), result);
                return result;
            }
        }

        private static object Invoke(Func<object> action)
        {
            try
            {
                return action();
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
