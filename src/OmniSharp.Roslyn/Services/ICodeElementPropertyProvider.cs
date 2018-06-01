using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Services
{
    public interface ICodeElementPropertyProvider
    {
        IEnumerable<(string name, object value)> ProvideProperties(ISymbol symbol);
    }
}
