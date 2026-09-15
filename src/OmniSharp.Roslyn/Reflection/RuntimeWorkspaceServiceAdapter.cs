#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.CodeAnalysis.Host;

namespace OmniSharp.Roslyn.Reflection
{
    public class RuntimeWorkspaceServiceAdapter : IWorkspaceService
    {
        private static readonly object s_gate = new();
        private static readonly Dictionary<Type, Type> s_adapterTypes = new();
        private Func<MethodInfo, object?[], object?>? _handler;

        public object? InvokeCore(MethodInfo method, object?[] arguments)
            => _handler?.Invoke(method, arguments)
               ?? (method.ReturnType == typeof(void) ? null : throw new InvalidOperationException(
                   $"No result was produced for Roslyn workspace service member '{method.Name}'."));

        internal static IWorkspaceService Create(Type interfaceType, Func<MethodInfo, object?[], object?> handler)
        {
            Type adapterType;
            lock (s_gate)
            {
                if (!s_adapterTypes.TryGetValue(interfaceType, out adapterType!))
                {
                    adapterType = BuildAdapterType(interfaceType);
                    s_adapterTypes.Add(interfaceType, adapterType);
                }
            }

            var adapter = (RuntimeWorkspaceServiceAdapter)Activator.CreateInstance(adapterType)!;
            adapter._handler = handler;
            return adapter;
        }

        private static Type BuildAdapterType(Type interfaceType)
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName($"OmniSharp.Roslyn.Adapter.{interfaceType.Name}"), AssemblyBuilderAccess.Run);
            assembly.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute)
                    .GetConstructor(new[] { typeof(string) })!,
                new object[] { interfaceType.Assembly.GetName().Name! }));
            var module = assembly.DefineDynamicModule("Adapter");
            var builder = module.DefineType(
                $"{interfaceType.Name}Adapter", TypeAttributes.Public | TypeAttributes.Sealed,
                typeof(RuntimeWorkspaceServiceAdapter), new[] { interfaceType });
            builder.DefineDefaultConstructor(MethodAttributes.Public);

            var invokeCore = typeof(RuntimeWorkspaceServiceAdapter).GetMethod(nameof(InvokeCore))!;
            var getMethodFromHandle = typeof(MethodBase).GetMethod(
                nameof(MethodBase.GetMethodFromHandle), new[] { typeof(RuntimeMethodHandle) })!;

            foreach (var interfaceMethod in interfaceType.GetMethods())
            {
                var parameters = interfaceMethod.GetParameters();
                if (parameters.Any(parameter => parameter.ParameterType.IsByRef))
                    throw new InvalidOperationException(
                        $"Roslyn workspace service method '{interfaceMethod}' uses unsupported by-ref parameters.");

                var method = builder.DefineMethod(
                    interfaceMethod.Name,
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final |
                    MethodAttributes.HideBySig | MethodAttributes.NewSlot,
                    interfaceMethod.ReturnType,
                    parameters.Select(parameter => parameter.ParameterType).ToArray());
                var il = method.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldtoken, interfaceMethod);
                il.Emit(OpCodes.Call, getMethodFromHandle);
                il.Emit(OpCodes.Castclass, typeof(MethodInfo));
                il.Emit(OpCodes.Ldc_I4, parameters.Length);
                il.Emit(OpCodes.Newarr, typeof(object));
                for (var i = 0; i < parameters.Length; i++)
                {
                    il.Emit(OpCodes.Dup);
                    il.Emit(OpCodes.Ldc_I4, i);
                    il.Emit(OpCodes.Ldarg, i + 1);
                    if (parameters[i].ParameterType.IsValueType)
                        il.Emit(OpCodes.Box, parameters[i].ParameterType);
                    il.Emit(OpCodes.Stelem_Ref);
                }

                il.Emit(OpCodes.Call, invokeCore);
                if (interfaceMethod.ReturnType == typeof(void))
                {
                    il.Emit(OpCodes.Pop);
                }
                else if (interfaceMethod.ReturnType.IsValueType)
                {
                    il.Emit(OpCodes.Unbox_Any, interfaceMethod.ReturnType);
                }
                else
                {
                    il.Emit(OpCodes.Castclass, interfaceMethod.ReturnType);
                }

                il.Emit(OpCodes.Ret);
                builder.DefineMethodOverride(method, interfaceMethod);
            }

            return builder.CreateTypeInfo()!.AsType();
        }
    }
}
