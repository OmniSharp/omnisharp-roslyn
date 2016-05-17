using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using OmniSharp.Models.V2;

namespace OmniSharp.Roslyn.CSharp.Services.Testing
{
    public class TestActionsProvider
    {
        public static IEnumerable<OmniSharpCodeAction> FindTestActions(Workspace workspace, GetCodeActionsRequest request)
        {
            yield return new OmniSharpCodeAction("test.action", "run test");
        }
    }
}