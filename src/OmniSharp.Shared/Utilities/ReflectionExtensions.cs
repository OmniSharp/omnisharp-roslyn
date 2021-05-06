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

        public static Lazy<MethodInfo> LazyGetProperty(this Lazy<Type> lazyType, string propertyName, bool getMethod)
        {
            if (lazyType == null)
            {
                throw new ArgumentNullException(nameof(lazyType));
            }

            return new(() =>
            {
                var type = lazyType.Value;
                var propertyInfo = type.GetProperty(propertyName);

                if (propertyInfo == null)
                {
                    throw new InvalidOperationException($"Could not get method '{propertyName}' on type '{type.FullName}'");
                }

                return getMethod ? propertyInfo.GetMethod : propertyInfo.SetMethod;
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

            return Activator.CreateInstance(
                lazyType.Value,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                args,
                culture: null);
        }

        public static T CreateInstance<T>(this Type type) where T : class
        {
            try
            {
                var defaultCtor = type.GetConstructor(new Type[] { });

                return defaultCtor != null
                    ? (T)Activator.CreateInstance(type)
                    : null;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create instrance of {type.FullName} in {type.AssemblyQualifiedName}.", ex);
            }
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

        public static T InvokeStatic<T>(this Lazy<Type> lazyType, string methodName, object[] args)
        {
            if (lazyType == null)
            {
                throw new ArgumentNullException(nameof(lazyType));
            }

            var method = lazyType.Value.GetMethod(methodName);
            if (method == null)
            {
                throw new InvalidOperationException($"Failed to retrieve method {method}");
            }

            return method.InvokeStatic<T>(args);
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
            return lazyMethodInfo.Value.InvokeStatic(args);
        }

        public static Lazy<FieldInfo> LazyGetField(this Lazy<Type> lazyType, string fieldName, BindingFlags bindingFlags)
        {
            if (lazyType == null)
            {
                throw new ArgumentNullException(nameof(lazyType));
            }

            return new Lazy<FieldInfo>(() =>
            {
                var type = lazyType.Value;
                var field = type.GetField(fieldName, bindingFlags);

                if (field == null)
                {
                    throw new InvalidOperationException($"Could not get method '{fieldName}' on type '{type.FullName}'");
                }

                return field;
            });
        }

        public static T GetValue<T>(this Lazy<FieldInfo> lazyFieldInfo, object o)
        {
            return (T)lazyFieldInfo.Value.GetValue(o);
        }
    }
}
