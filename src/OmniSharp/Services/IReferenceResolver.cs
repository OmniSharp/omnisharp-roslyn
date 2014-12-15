using Microsoft.CodeAnalysis;
using System;

namespace OmniSharp.Services
{
    interface IReferenceResolver
    {
        MetadataReference Resolve(string name);
    }
}