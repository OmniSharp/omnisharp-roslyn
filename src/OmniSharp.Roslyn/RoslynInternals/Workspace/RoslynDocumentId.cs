#nullable enable

using System;
using System.Reflection;
using Microsoft.CodeAnalysis;
using OmniSharp.Roslyn.Reflection;

namespace OmniSharp.Roslyn.RoslynInternals.Workspace
{
    public static class RoslynDocumentId
    {
        private static readonly MethodInfo s_create = RoslynReflection.GetMethod(typeof(DocumentId), "CreateFromSerialized",
            m => m.IsStatic && m.GetParameters().Length == 4);

        public static DocumentId CreateFromSerialized(ProjectId projectId, Guid id, bool isSourceGenerated, string? debugName)
            => RoslynReflection.Invoke<DocumentId>(s_create, null, projectId, id, isSourceGenerated, debugName);
    }
}
