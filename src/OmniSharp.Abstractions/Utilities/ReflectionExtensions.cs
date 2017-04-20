using System;
using System.Reflection;

namespace OmniSharp.Utilities
{
    public static class ReflectionExtensions
    {
        public static Lazy<Type> LazyGetType(this Lazy<Assembly> lazyAssembly, string typeName)
        {
            if (lazyAssembly == null)
            {
                throw new ArgumentNullException(nameof(lazyAssembly));
            }

            return new Lazy<Type>(() =>
            {
                var type = lazyAssembly.Value.GetType(typeName);

                if (type == null)
                {
                    throw new InvalidOperationException($"Could not get type '{typeName}'");
                }

                return type;
            });
        }

        public static Lazy<MethodInfo> LazyGetMethod(this Lazy<Type> lazyType, string methodName)
        {
            if (lazyType == null)
            {
                throw new ArgumentNullException(nameof(lazyType));
            }

            return new Lazy<MethodInfo>(() =>
            {
                var type = lazyType.Value;
                var methodInfo = type.GetMethod(methodName);

                if (methodInfo == null)
                {
                    throw new InvalidOperationException($"Could not get method '{methodName}' on type '{type.FullName}'");
                }

                return methodInfo;
            });
        }

        public static Lazy<MethodInfo> LazyGetMethod(this Lazy<Type> lazyType, string methodName, BindingFlags bindingFlags)
        {
            if (lazyType == null)
            {
                throw new ArgumentNullException(nameof(lazyType));
            }

            return new Lazy<MethodInfo>(() =>
            {
                var type = lazyType.Value;
                var methodInfo = type.GetMethod(methodName, bindingFlags);

                if (methodInfo == null)
                {
                    throw new InvalidOperationException($"Could not get method '{methodName}' on type '{type.FullName}'");
                }

                return methodInfo;
            });
        }

        public static MethodInfo GetMethod(this Lazy<Type> lazyType, string methodName)
        {
            if (lazyType == null)
            {
                throw new ArgumentNullException(nameof(lazyType));
            }

            var type = lazyType.Value;
            var methodInfo = type.GetMethod(methodName);

            if (methodInfo == null)
            {
                throw new InvalidOperationException($"Could not get method '{methodName}' on type '{type.FullName}'");
            }

            return methodInfo;
        }

        public static MethodInfo GetMethod(this Lazy<Type> lazyType, string methodName, BindingFlags bindingFlags)
        {
            if (lazyType == null)
            {
                throw new ArgumentNullException(nameof(lazyType));
            }

            var type = lazyType.Value;
            var methodInfo = type.GetMethod(methodName, bindingFlags);

            if (methodInfo == null)
            {
                throw new InvalidOperationException($"Could not get method '{methodName}' on type '{type.FullName}'");
            }

            return methodInfo;
        }

        public static object CreateInstance(this Lazy<Type> lazyType, params object[] args)
        {
            if (lazyType == null)
            {
                throw new ArgumentNullException(nameof(lazyType));
            }

            return Activator.CreateInstance(lazyType.Value, args);
        }

        public static T Invoke<T>(this MethodInfo methodInfo, object obj, object[] args)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException(nameof(methodInfo));
            }

            return (T)methodInfo.Invoke(obj, args);
        }

        public static T Invoke<T>(this Lazy<MethodInfo> lazyMethodInfo, object obj, object[] args)
        {
            if (lazyMethodInfo == null)
            {
                throw new ArgumentNullException(nameof(lazyMethodInfo));
            }

            return (T)lazyMethodInfo.Value.Invoke(obj, args);
        }

        public static T InvokeStatic<T>(this MethodInfo methodInfo, object[] args)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException(nameof(methodInfo));
            }

            return (T)methodInfo.Invoke(null, args);
        }

        public static T InvokeStatic<T>(this Lazy<MethodInfo> lazyMethodInfo, object[] args)
        {
            if (lazyMethodInfo == null)
            {
                throw new ArgumentNullException(nameof(lazyMethodInfo));
            }

            return lazyMethodInfo.Value.InvokeStatic<T>(args);
        }

        public static object InvokeStatic(this MethodInfo methodInfo, object[] args)
        {
            return methodInfo.Invoke(null, args);
        }

        public static object InvokeStatic(this Lazy<MethodInfo> lazyMethodInfo, object[] args)
        {
            return lazyMethodInfo.InvokeStatic(args);
        }
    }
}
