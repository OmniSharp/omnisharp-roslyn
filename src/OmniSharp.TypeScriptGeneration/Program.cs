using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNet.Mvc;
using OmniSharp.Models;
using OmniSharp.Stdio;
using OmniSharp.Stdio.Protocol;
using TypeLite;
using TypeLite.TsModels;

namespace OmniSharp.TypeScriptGeneration
{
    public class Program
    {

        public void Main(string[] args)
        {
            var path = string.Empty;
            if (args.Length == 1)
            {
                path = args[0];
            }


            var fluent = TypeScript.Definitions();
            fluent.ScriptGenerator.IndentationString = "    ";

            fluent.WithMemberTypeFormatter(TsFluentFormatters.FormatPropertyType);
            fluent.WithMemberFormatter(TsFluentFormatters.FormatPropertyName);
            //definitions.WithTypeFormatter(IgnoreInvalidTypes);

            foreach (var model in GetApplicableTypes())
            {
                fluent.For(model);
            }

            var tsModel = fluent.ModelBuilder.Build();
            foreach (var @class in tsModel.Classes.Where(z => z.Module.Name.StartsWith("System", StringComparison.Ordinal)))
            {
                @class.IsIgnored = true;
            }

            var result = fluent.Generate();

            result = string.Join("\n", result, OmnisharpControllerExtractor.GetInterface());
            if (!string.IsNullOrWhiteSpace(path))
            {
                File.WriteAllText(Path.Combine(path, "omnisharp-server.d.ts"), result);
            }
            else
            {
                Console.Write(result);
                Console.ReadLine();
            }

        }

        private IEnumerable<Type> GetApplicableTypes()
        {
            var models = typeof(Request).Assembly.DefinedTypes
                .Where(z => z.IsPublic && z.FullName.StartsWith(OmnisharpControllerExtractor.InferNamespace(typeof(Request)), StringComparison.Ordinal));
            var stdioProtocol = typeof(Packet).Assembly.DefinedTypes
                .Where(z => z.IsPublic && z.FullName.StartsWith(OmnisharpControllerExtractor.InferNamespace(typeof(Packet)), StringComparison.Ordinal));

            return models.Union(stdioProtocol).ToArray();
        }
    }
}
