using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.AspNet.Mvc;
using Microsoft.Framework.DependencyInjection;

namespace OmniSharp.Stdio.Transport
{
    public static class Controllers
    {
        private readonly static IDictionary<string, MethodInfo> routes;

        private readonly static IEnumerable<Type> types;

        static Controllers()
        {
            routes = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);
            types = new[] {
                typeof(OmnisharpController),
                typeof(ProjectSystemController),
                typeof(CodeActionController)
            };

            foreach (var type in types)
            {
                foreach (var method in type.GetMethods())
                {
                    var attribute = method.GetCustomAttribute<HttpPostAttribute>();
                    if (attribute == null)
                    {
                        continue;
                    }
                    if (method.GetParameters().Length > 1)
                    {
                        continue;
                    }
                    
                    routes[attribute.Template.TrimStart('/')] = method;
                }
            }
        }

        public static MethodInfo LookUp(string command)
        {
            MethodInfo result = null;
            routes.TryGetValue(command, out result);
            return result;
        }
        
        public static void AddControllers(this IServiceCollection collection)
        {
            foreach (var t in types)
            {
                collection.AddTransient(t);
            }
        }
    }
}