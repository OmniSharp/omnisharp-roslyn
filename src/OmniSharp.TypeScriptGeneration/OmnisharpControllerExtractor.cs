using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using OmniSharp.Models;

namespace OmniSharp.TypeScriptGeneration
{
    public static class OmnisharpControllerExtractor
    {
        public static string GetInterface()
        {
            var methods = "        " + string.Join("\n        ", GetInterfaceMethods()) + "\n";

            return $"declare module {nameof(OmniSharp)} {{\n{ContextInterface}    interface Api {{\n{methods}    }}\n}}";
        }

        private static string ContextInterface = "    interface Context<TRequest, TResponse>\n    {\n        request: TRequest;\n        response: TResponse;\n    }\n\n";

        private static IEnumerable<string> GetInterfaceMethods()
        {
            var methods = GetControllerMethods().ToArray();
            foreach (var method in methods)
            {
                var observeName = $"observe{method.Action[0].ToString().ToUpper()}{method.Action.Substring(1)}";

                var requestType = method.RequestType;
                if (method.RequestArray)
                    requestType += "[]";

                var returnType = method.ReturnType;
                if (method.ReturnArray)
                    returnType += "[]";

                if (method.RequestType != null)
                {
                    yield return $"{method.Action}(request: {requestType}): Rx.Observable<{returnType}>;";
                    yield return $"{method.Action}Promise(request: {requestType}): Rx.IPromise<{returnType}>;";
                    yield return $"{observeName}: Rx.Observable<Context<{requestType}, {returnType}>>;";
                }
                else
                {
                    yield return $"{method.Action}(): Rx.Observable<{returnType}>;";
                    yield return $"{method.Action}Promise(): Rx.IPromise<{returnType}>;";
                    yield return $"{observeName}: Rx.Observable<{returnType}>;";
                }
            }
        }

        class MethodResult
        {
            public string Action { get; set; }
            public string RequestType { get; set; }
            public bool RequestArray { get; set; }
            public string ReturnType { get; set; }
            public bool ReturnArray { get; set; }
        }

        private static IEnumerable<MethodResult> GetControllerMethods()
        {
            var methods = typeof(OmnisharpController).Assembly.GetTypes()
                .SelectMany(z => z.GetTypeInfo()
                    .DeclaredMethods.Where(x =>
                        x.GetCustomAttributes<HttpPostAttribute>().Any()));

            foreach (var method in methods.Where(z => z.IsPublic))
            {
                var attribute = method.GetCustomAttribute<HttpPostAttribute>();
                var parameters = method.GetParameters();
                var param = parameters.Length == 1 ? parameters[0].ParameterType : null;


                var paramType = param;
                var paramArray = false;
                if (paramType != null && paramType.Name.StartsWith(nameof(IEnumerable), StringComparison.Ordinal))
                {
                    paramArray = true;
                    paramType = paramType.GetGenericArguments().First();
                }

                string paramString = "any";
                if (paramType != null && paramType.FullName.StartsWith(InferNamespace(typeof(Request)), StringComparison.Ordinal))
                {
                    paramString = paramType.FullName;
                }

                if (paramType == typeof(Boolean))
                {
                    paramString = nameof(Boolean).ToLowerInvariant();
                }

                var returnType = method.ReturnType;
                var returnsArray = false;
                if (returnType.Name.StartsWith(nameof(Task), StringComparison.Ordinal))
                {
                    returnType = returnType.GetGenericArguments().First();
                }
                if (returnType.Name.StartsWith(nameof(IEnumerable), StringComparison.Ordinal))
                {
                    returnsArray = true;
                    returnType = returnType.GetGenericArguments().First();
                }

                string returnString = "any";
                if (returnType != null && returnType.FullName.StartsWith(InferNamespace(typeof(Request)), StringComparison.Ordinal))
                {
                    returnString = returnType.FullName;
                }

                if (returnType == typeof(Boolean))
                {
                    returnString = nameof(Boolean).ToLowerInvariant();
                }

                yield return new MethodResult()
                {
                    RequestType = paramString,
                    RequestArray = paramArray,
                    ReturnType = returnString,
                    ReturnArray = returnsArray,
                    Action = attribute.Template.TrimStart('/')
                };
            }
        }

        internal static string InferNamespace(Type type)
        {
            var pieces = type.FullName.Split('.');
            return string.Join(".", pieces.Take(pieces.Length - 1)) + ".";
        }
    }
}
